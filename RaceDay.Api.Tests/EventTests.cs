using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class EventTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Organiser_CanCreateEvent()
    {
        var (organiser, userId) = await Api.SignInAsync(factory, "Organiser");

        var res = await organiser.PostAsJsonAsync("/api/events", Api.EventBody());

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.NotNull(res.Headers.Location);
        var created = await res.Content.ReadFromJsonAsync<EventDetailResponse>();
        Assert.Equal(userId, created!.OrganiserId);
        Assert.Equal("Published", created.Status);
    }

    [Fact]
    public async Task Organiser_CreateEvent_WithPastDate_ReturnsBadRequest()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");

        var res = await organiser.PostAsJsonAsync("/api/events", Api.EventBody(daysAhead: -5, closesInDays: -6));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CreateEvent_WithUnknownProvince_ReturnsBadRequest()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");

        var res = await organiser.PostAsJsonAsync("/api/events", Api.EventBody(province: "Atlantis"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CreateEvent_WithRegistrationClosingAfterEvent_ReturnsBadRequest()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");

        var res = await organiser.PostAsJsonAsync("/api/events", Api.EventBody(daysAhead: 10, closesInDays: 20));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CreateEvent_WithMissingName_ReturnsBadRequest()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");

        var res = await organiser.PostAsJsonAsync("/api/events", new { description = "no name" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");
        var ev = await Api.CreateEventAsync(organiser);

        var res = await organiser.PutAsJsonAsync($"/api/events/{ev.EventId}", Api.EventBody(name: "Renamed Event"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Renamed Event", (await res.Content.ReadFromJsonAsync<EventDetailResponse>())!.Name);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent()
    {
        var (owner, _) = await Api.SignInAsync(factory, "Organiser");
        var (other, _) = await Api.SignInAsync(factory, "Organiser");
        var ev = await Api.CreateEventAsync(owner);

        var res = await other.PutAsJsonAsync($"/api/events/{ev.EventId}", Api.EventBody());

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");
        var ev = await Api.CreateEventAsync(organiser);

        var delete = await organiser.DeleteAsync($"/api/events/{ev.EventId}");
        var get = await organiser.GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent()
    {
        var (owner, _) = await Api.SignInAsync(factory, "Organiser");
        var (other, _) = await Api.SignInAsync(factory, "Organiser");
        var ev = await Api.CreateEventAsync(owner);

        var res = await other.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteEvent_WithConfirmedEnrolments()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");
        var (participant, _) = await Api.SignInAsync(factory, "Participant");
        var ev = await Api.CreateEventAsync(organiser);
        var category = await Api.CreateCategoryAsync(organiser, ev.EventId, fee: 100m);
        await Api.EnrolOkAsync(participant, ev.EventId, category.CategoryId, paid: 100m);

        var res = await organiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CanBrowsePublishedEvents()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");
        var token = Guid.NewGuid().ToString("N");
        await Api.CreateEventAsync(organiser, Api.EventBody(name: $"Browse {token}"));

        var res = await factory.CreateClient().GetAsync($"/api/events?search={token}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var page = await res.Content.ReadFromJsonAsync<PagedResponse<EventListItem>>();
        Assert.Equal(1, page!.TotalCount);
    }

    [Fact]
    public async Task DraftEvent_IsHiddenFromPublic_ButVisibleToItsOrganiser()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");
        var token = Guid.NewGuid().ToString("N");
        var draft = await Api.CreateEventAsync(organiser, Api.EventBody(name: $"Draft {token}", status: "Draft"));

        var list = await (await factory.CreateClient().GetAsync($"/api/events?search={token}")).Content.ReadFromJsonAsync<PagedResponse<EventListItem>>();
        var publicGet = await factory.CreateClient().GetAsync($"/api/events/{draft.EventId}");
        var ownerGet = await organiser.GetAsync($"/api/events/{draft.EventId}");

        Assert.Equal(0, list!.TotalCount);
        Assert.Equal(HttpStatusCode.NotFound, publicGet.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerGet.StatusCode);
    }

    [Fact]
    public async Task GetEvent_ThatDoesNotExist_ReturnsNotFound()
    {
        var res = await factory.CreateClient().GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanPublishDraft_ButCompletedCannotGoBackToDraft()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");
        var ev = await Api.CreateEventAsync(organiser, Api.EventBody(status: "Draft"));

        var publish = await organiser.PatchJsonAsync($"/api/events/{ev.EventId}/status", new { status = "Published" });
        var complete = await organiser.PatchJsonAsync($"/api/events/{ev.EventId}/status", new { status = "Completed" });
        var back = await organiser.PatchJsonAsync($"/api/events/{ev.EventId}/status", new { status = "Draft" });

        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, back.StatusCode);
    }

    [Fact]
    public async Task Organiser_MineEndpoint_ListsOnlyOwnEvents()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");
        var (other, _) = await Api.SignInAsync(factory, "Organiser");
        var mine = await Api.CreateEventAsync(organiser);
        var theirs = await Api.CreateEventAsync(other);

        var rows = await (await organiser.GetAsync("/api/events/mine")).Content.ReadFromJsonAsync<List<MyEventResponse>>();

        Assert.Contains(rows!, r => r.EventId == mine.EventId);
        Assert.DoesNotContain(rows!, r => r.EventId == theirs.EventId);
    }

    [Fact]
    public async Task Organiser_CanAddCategory_AndDuplicateNameIsRejected()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");
        var ev = await Api.CreateEventAsync(organiser);
        var category = await Api.CreateCategoryAsync(organiser, ev.EventId, name: "Senior 21 km");

        var duplicate = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Senior 21 km", distanceKm = 21m, entryFee = 200m, startTime = "06:30:00" });
        var list = await (await factory.CreateClient().GetAsync($"/api/events/{ev.EventId}/categories")).Content.ReadFromJsonAsync<List<CategoryResponse>>();

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains(list!, c => c.CategoryId == category.CategoryId);
    }

    [Fact]
    public async Task Organiser_CreateCategory_WithZeroDistance_ReturnsBadRequest()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");
        var ev = await Api.CreateEventAsync(organiser);

        var res = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Bad", distanceKm = 0m, entryFee = 10m, startTime = "06:30:00" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}

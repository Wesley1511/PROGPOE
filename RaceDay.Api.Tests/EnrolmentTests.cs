using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class EnrolmentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(HttpClient Organiser, EventDetailResponse Event, CategoryResponse Category)> SetupAsync(
        decimal fee = 100m, int? max = null, int minimumAge = 0, object? eventBody = null)
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");
        var ev = await Api.CreateEventAsync(organiser, eventBody);
        var category = await Api.CreateCategoryAsync(organiser, ev.EventId, fee, max, minimumAge);
        return (organiser, ev, category);
    }

    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecorded()
    {
        var (_, ev, category) = await SetupAsync();
        var (participant, _) = await Api.SignInAsync(factory, "Participant");

        var res = await Api.EnrolAsync(participant, ev.EventId, category.CategoryId, paid: 100m);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<EnrolmentResponse>();
        Assert.Equal("Confirmed", created!.Status);
        Assert.False(string.IsNullOrWhiteSpace(created.RaceNumber));

        var mine = await participant.GetFromJsonAsync<List<MyEnrolmentRow>>("/api/enrolments/me");
        var row = Assert.Single(mine!, r => r.EnrolmentId == created.EnrolmentId);
        Assert.Equal(ev.EventId, row.EventId);
        Assert.Equal(category.Name, row.CategoryName);
    }

    [Fact]
    public async Task Enrol_WithPartialPayment_IsPending()
    {
        var (_, ev, category) = await SetupAsync(fee: 100m);
        var (participant, _) = await Api.SignInAsync(factory, "Participant");

        var created = await Api.EnrolOkAsync(participant, ev.EventId, category.CategoryId, paid: 0m);

        Assert.Equal("Pending", created.Status);
    }

    [Fact]
    public async Task Enrol_RaceNumbersAreUniquePerEvent()
    {
        var (_, ev, category) = await SetupAsync();
        var (first, _) = await Api.SignInAsync(factory, "Participant");
        var (second, _) = await Api.SignInAsync(factory, "Participant");

        var a = await Api.EnrolOkAsync(first, ev.EventId, category.CategoryId);
        var b = await Api.EnrolOkAsync(second, ev.EventId, category.CategoryId);

        Assert.NotEqual(a.RaceNumber, b.RaceNumber);
    }

    [Fact]
    public async Task Enrol_Twice_ReturnsConflict()
    {
        var (_, ev, category) = await SetupAsync();
        var (participant, _) = await Api.SignInAsync(factory, "Participant");
        await Api.EnrolOkAsync(participant, ev.EventId, category.CategoryId);

        var res = await Api.EnrolAsync(participant, ev.EventId, category.CategoryId);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithCategoryFromAnotherEvent_ReturnsBadRequest()
    {
        var (organiser, ev, _) = await SetupAsync();
        var otherEvent = await Api.CreateEventAsync(organiser);
        var otherCategory = await Api.CreateCategoryAsync(organiser, otherEvent.EventId);
        var (participant, _) = await Api.SignInAsync(factory, "Participant");

        var res = await Api.EnrolAsync(participant, ev.EventId, otherCategory.CategoryId);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithUnknownCategory_ReturnsNotFound()
    {
        var (_, ev, _) = await SetupAsync();
        var (participant, _) = await Api.SignInAsync(factory, "Participant");

        var res = await Api.EnrolAsync(participant, ev.EventId, 999999);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_BelowMinimumAge_ReturnsBadRequest()
    {
        var (_, ev, category) = await SetupAsync(minimumAge: 18);
        var (child, _) = await Api.SignInAsync(factory, "Participant", dob: "2015-01-01");

        var res = await Api.EnrolAsync(child, ev.EventId, category.CategoryId);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenCategoryIsFull_ReturnsConflict()
    {
        var (_, ev, category) = await SetupAsync(max: 1);
        var (first, _) = await Api.SignInAsync(factory, "Participant");
        var (second, _) = await Api.SignInAsync(factory, "Participant");
        await Api.EnrolOkAsync(first, ev.EventId, category.CategoryId);

        var res = await Api.EnrolAsync(second, ev.EventId, category.CategoryId);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_AfterRegistrationClosed_ReturnsConflict()
    {
        var (_, ev, category) = await SetupAsync(eventBody: Api.EventBody(daysAhead: 30, closesInDays: -1));
        var (participant, _) = await Api.SignInAsync(factory, "Participant");

        var res = await Api.EnrolAsync(participant, ev.EventId, category.CategoryId);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_IntoDraftEvent_ReturnsConflict()
    {
        var (_, ev, category) = await SetupAsync(eventBody: Api.EventBody(status: "Draft"));
        var (participant, _) = await Api.SignInAsync(factory, "Participant");

        var res = await Api.EnrolAsync(participant, ev.EventId, category.CategoryId);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanSeeEnrolments_ForOwnEvent_ButOtherOrganiserCannot()
    {
        var (organiser, ev, category) = await SetupAsync();
        var (other, _) = await Api.SignInAsync(factory, "Organiser");
        var (participant, _) = await Api.SignInAsync(factory, "Participant");
        var enrolment = await Api.EnrolOkAsync(participant, ev.EventId, category.CategoryId);

        var own = await organiser.GetAsync($"/api/events/{ev.EventId}/enrolments");
        var foreign = await other.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var rows = await own.Content.ReadFromJsonAsync<List<EventEnrolmentRow>>();
        Assert.Contains(rows!, r => r.EnrolmentId == enrolment.EnrolmentId && r.CategoryName == category.Name);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    [Fact]
    public async Task EnrolmentDetail_IsVisibleToOwner_ButNotToOtherParticipants()
    {
        var (_, ev, category) = await SetupAsync();
        var (owner, _) = await Api.SignInAsync(factory, "Participant");
        var (stranger, _) = await Api.SignInAsync(factory, "Participant");
        var enrolment = await Api.EnrolOkAsync(owner, ev.EventId, category.CategoryId);

        var ownRes = await owner.GetAsync($"/api/enrolments/{enrolment.EnrolmentId}");
        var strangerRes = await stranger.GetAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.OK, ownRes.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, strangerRes.StatusCode);
    }

    [Fact]
    public async Task Participant_CanChangeCategory_WithinSameEvent()
    {
        var (organiser, ev, first) = await SetupAsync();
        var second = await Api.CreateCategoryAsync(organiser, ev.EventId, name: "Second Category");
        var (participant, _) = await Api.SignInAsync(factory, "Participant");
        var enrolment = await Api.EnrolOkAsync(participant, ev.EventId, first.CategoryId);

        var res = await participant.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/category", new { categoryId = second.CategoryId });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Second Category", (await res.Content.ReadFromJsonAsync<EnrolmentResponse>())!.CategoryName);
    }

    [Fact]
    public async Task Participant_CannotChangeCategory_ToAnotherEvent()
    {
        var (organiser, ev, first) = await SetupAsync();
        var otherEvent = await Api.CreateEventAsync(organiser);
        var foreign = await Api.CreateCategoryAsync(organiser, otherEvent.EventId);
        var (participant, _) = await Api.SignInAsync(factory, "Participant");
        var enrolment = await Api.EnrolOkAsync(participant, ev.EventId, first.CategoryId);

        var res = await participant.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/category", new { categoryId = foreign.CategoryId });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanConfirmPendingEnrolment()
    {
        var (organiser, ev, category) = await SetupAsync(fee: 100m);
        var (participant, _) = await Api.SignInAsync(factory, "Participant");
        var enrolment = await Api.EnrolOkAsync(participant, ev.EventId, category.CategoryId, paid: 0m);

        var res = await organiser.PatchJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Confirmed", (await res.Content.ReadFromJsonAsync<EnrolmentStatusResponse>())!.Status);
    }

    [Fact]
    public async Task Participant_CanWithdraw_AndThenEnrolAgain()
    {
        var (_, ev, category) = await SetupAsync();
        var (participant, _) = await Api.SignInAsync(factory, "Participant");
        var enrolment = await Api.EnrolOkAsync(participant, ev.EventId, category.CategoryId);

        var withdraw = await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");
        var mine = await participant.GetFromJsonAsync<List<MyEnrolmentRow>>("/api/enrolments/me");
        var again = await Api.EnrolAsync(participant, ev.EventId, category.CategoryId);

        Assert.Equal(HttpStatusCode.NoContent, withdraw.StatusCode);
        Assert.Equal("Withdrawn", mine!.Single(r => r.EnrolmentId == enrolment.EnrolmentId).Status);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotWithdraw_AnotherParticipantsEntry()
    {
        var (_, ev, category) = await SetupAsync();
        var (owner, _) = await Api.SignInAsync(factory, "Participant");
        var (stranger, _) = await Api.SignInAsync(factory, "Participant");
        var enrolment = await Api.EnrolOkAsync(owner, ev.EventId, category.CategoryId);

        var res = await stranger.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Enrolment_ReducesPlacesRemaining()
    {
        var (_, ev, category) = await SetupAsync(max: 5);
        var (participant, _) = await Api.SignInAsync(factory, "Participant");
        await Api.EnrolOkAsync(participant, ev.EventId, category.CategoryId);

        var categories = await factory.CreateClient().GetFromJsonAsync<List<CategoryResponse>>($"/api/events/{ev.EventId}/categories");

        Assert.Equal(4, categories!.Single(c => c.CategoryId == category.CategoryId).PlacesRemaining);
    }
}

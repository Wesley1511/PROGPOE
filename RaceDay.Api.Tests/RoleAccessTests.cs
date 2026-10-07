using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Api.Tests;

public class RoleAccessTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url) =>
        client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(new { }) });

    [Theory]
    [InlineData("POST", "/api/events")]
    [InlineData("PUT", "/api/events/1")]
    [InlineData("DELETE", "/api/events/1")]
    [InlineData("PATCH", "/api/events/1/status")]
    [InlineData("GET", "/api/events/mine")]
    [InlineData("GET", "/api/events/1/enrolments")]
    [InlineData("POST", "/api/events/1/categories")]
    [InlineData("PUT", "/api/categories/1")]
    [InlineData("DELETE", "/api/categories/1")]
    [InlineData("POST", "/api/categories/1/waypoints")]
    [InlineData("PATCH", "/api/enrolments/1/status")]
    [InlineData("POST", "/api/enrolments/1/result")]
    [InlineData("PUT", "/api/results/1")]
    [InlineData("DELETE", "/api/results/1")]
    [InlineData("POST", "/api/events/1/results/bulk")]
    [InlineData("GET", "/api/users/1")]
    public async Task Participant_IsForbidden_FromOrganiserEndpoints(string method, string url)
    {
        var (participant, _) = await Api.SignInAsync(factory, "Participant");

        var res = await SendAsync(participant, method, url);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/events/1/enrolments")]
    [InlineData("GET", "/api/enrolments/me")]
    [InlineData("PUT", "/api/enrolments/1/category")]
    [InlineData("DELETE", "/api/enrolments/1")]
    [InlineData("GET", "/api/results/me")]
    [InlineData("GET", "/api/results/me/stats")]
    [InlineData("PUT", "/api/users/me/profile")]
    public async Task Organiser_IsForbidden_FromParticipantEndpoints(string method, string url)
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");

        var res = await SendAsync(organiser, method, url);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/events")]
    [InlineData("GET", "/api/events/mine")]
    [InlineData("POST", "/api/events/1/enrolments")]
    [InlineData("GET", "/api/enrolments/me")]
    [InlineData("GET", "/api/enrolments/1")]
    [InlineData("GET", "/api/users/me")]
    [InlineData("GET", "/api/results/me")]
    [InlineData("POST", "/api/enrolments/1/result")]
    public async Task Anonymous_IsUnauthorized_OnProtectedEndpoints(string method, string url)
    {
        var res = await SendAsync(factory.CreateClient(), method, url);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData("/api/events")]
    [InlineData("/api/events/999999")]
    [InlineData("/api/auth/roles")]
    [InlineData("/api/health")]
    public async Task Anonymous_CanReachPublicEndpoints(string url)
    {
        var res = await factory.CreateClient().GetAsync(url);

        Assert.NotEqual(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewParticipantContact_OnlyForOwnEvents()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");
        var (otherOrganiser, _) = await Api.SignInAsync(factory, "Organiser");
        var (participant, participantId) = await Api.SignInAsync(factory, "Participant");
        var ev = await Api.CreateEventAsync(organiser);
        var category = await Api.CreateCategoryAsync(organiser, ev.EventId);
        await Api.EnrolOkAsync(participant, ev.EventId, category.CategoryId);

        var own = await organiser.GetAsync($"/api/users/{participantId}");
        var other = await otherOrganiser.GetAsync($"/api/users/{participantId}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    [Fact]
    public async Task Participant_CanUpdateOwnProfile()
    {
        var (participant, _) = await Api.SignInAsync(factory, "Participant");

        var res = await participant.PutAsJsonAsync("/api/users/me/profile", new
        {
            dateOfBirth = "1985-02-02",
            gender = "Female",
            clubName = "New Club",
            tShirtSize = "S",
            emergencyContactName = "Someone",
            emergencyContactPhone = "0820000000"
        });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Participant_UpdateProfile_WithFutureBirthDate_ReturnsBadRequest()
    {
        var (participant, _) = await Api.SignInAsync(factory, "Participant");

        var res = await participant.PutAsJsonAsync("/api/users/me/profile", new
        {
            dateOfBirth = Api.Today.AddYears(1),
            gender = "Female",
            emergencyContactName = "Someone",
            emergencyContactPhone = "0820000000"
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
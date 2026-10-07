using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class ResultTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(HttpClient Organiser, HttpClient Participant, EventDetailResponse Event, EnrolmentResponse Enrolment)> SetupAsync()
    {
        var (organiser, _) = await Api.SignInAsync(factory, "Organiser");
        var (participant, _) = await Api.SignInAsync(factory, "Participant");
        var ev = await Api.CreateEventAsync(organiser);
        var category = await Api.CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await Api.EnrolOkAsync(participant, ev.EventId, category.CategoryId);
        return (organiser, participant, ev, enrolment);
    }

    [Fact]
    public async Task Organiser_CanCaptureResult_AndParticipantCanSeeIt()
    {
        var (organiser, participant, _, enrolment) = await SetupAsync();

        var create = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTimeSeconds = 3600, overallPosition = 5, categoryPosition = 2, status = "Finished" });
        var mine = await participant.GetFromJsonAsync<List<MyResultRow>>("/api/results/me");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var row = Assert.Single(mine!);
        Assert.Equal(3600, row.FinishTimeSeconds);
        Assert.Equal(5, row.OverallPosition);
        Assert.Equal(360, row.PaceSecondsPerKm);
        Assert.Equal(1, row.TotalFinishers);
    }

    [Fact]
    public async Task FinishedResult_WithoutTime_ReturnsBadRequest()
    {
        var (organiser, _, _, enrolment) = await SetupAsync();

        var res = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", new { status = "Finished" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Result_WithNegativeTime_ReturnsBadRequest()
    {
        var (organiser, _, _, enrolment) = await SetupAsync();

        var res = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTimeSeconds = -10, status = "Finished" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Result_Twice_ReturnsConflict()
    {
        var (organiser, _, _, enrolment) = await SetupAsync();
        var body = new { finishTimeSeconds = 3000, overallPosition = 1, categoryPosition = 1, status = "Finished" };
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", body);

        var res = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", body);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotCaptureResult()
    {
        var (_, _, _, enrolment) = await SetupAsync();
        var (other, _) = await Api.SignInAsync(factory, "Organiser");

        var res = await other.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTimeSeconds = 3000, status = "Finished" });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCorrectAndDeleteResult()
    {
        var (organiser, _, _, enrolment) = await SetupAsync();
        var created = await (await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTimeSeconds = 3000, overallPosition = 3, categoryPosition = 3, status = "Finished" }))
            .Content.ReadFromJsonAsync<ResultResponse>();

        var update = await organiser.PutAsJsonAsync($"/api/results/{created!.ResultId}",
            new { finishTimeSeconds = 2900, overallPosition = 2, categoryPosition = 2, status = "Finished" });
        var delete = await organiser.DeleteAsync($"/api/results/{created.ResultId}");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(2900, (await update.Content.ReadFromJsonAsync<ResultResponse>())!.FinishTimeSeconds);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task PublicResultsBoard_ListsCapturedResults()
    {
        var (organiser, _, ev, enrolment) = await SetupAsync();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTimeSeconds = 3000, overallPosition = 1, categoryPosition = 1, status = "Finished" });

        var board = await factory.CreateClient().GetFromJsonAsync<List<ResultsBoardRow>>($"/api/events/{ev.EventId}/results");

        Assert.Single(board!);
        Assert.Equal(enrolment.RaceNumber, board![0].RaceNumber);
    }

    [Fact]
    public async Task BulkUpload_ImportsKnownRaceNumbers_AndReportsUnknownOnes()
    {
        var (organiser, _, ev, enrolment) = await SetupAsync();

        var res = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/results/bulk", new[]
        {
            new { raceNumber = enrolment.RaceNumber, finishTimeSeconds = 3300, overallPosition = 1, categoryPosition = 1, status = "Finished" },
            new { raceNumber = "ZZZ999", finishTimeSeconds = 3400, overallPosition = 2, categoryPosition = 2, status = "Finished" }
        });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var summary = await res.Content.ReadFromJsonAsync<BulkResultResponse>();
        Assert.Equal(1, summary!.Imported);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal("ZZZ999", summary.Errors[0].RaceNumber);
    }

    [Fact]
    public async Task Participant_Stats_ReflectFinishedResults()
    {
        var (organiser, participant, _, enrolment) = await SetupAsync();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTimeSeconds = 3600, overallPosition = 5, categoryPosition = 2, status = "Finished" });

        var stats = await participant.GetFromJsonAsync<StatsResponse>("/api/results/me/stats");

        Assert.Equal(1, stats!.EventsEntered);
        Assert.Equal(1, stats.EventsCompleted);
        Assert.Equal(10m, stats.TotalDistanceKm);
        Assert.Equal(360, stats.AveragePaceSecondsPerKm);
        Assert.Single(stats.PersonalBests);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace RaceDay.Api.Controllers;

[Route("api")]
public class ResultsController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>Captures the finish time and positions for one entry after the race.</summary>
    [HttpPost("enrolments/{enrolmentId:int}/result"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<ResultResponse>(Status201Created)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    [ProducesResponseType(Status409Conflict)]
    public async Task<IActionResult> Create(int enrolmentId, ResultRequest req)
    {
        var enrolment = await db.Enrolments.Include(n => n.Result).Include(n => n.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(n => n.EnrolmentId == enrolmentId);
        if (enrolment is null) return Fail(404, "Enrolment not found.");
        if (enrolment.Category.Event.OrganiserId != CurrentUserId) return Fail(403, "You do not organise this event.");

        var error = Validate(req.Status, req.FinishTimeSeconds, req.OverallPosition, req.CategoryPosition);
        if (error is not null) return Fail(400, error);
        if (enrolment.Result is not null) return Fail(409, "A result already exists for this entry.");

        var result = new Result { Enrolment = enrolment, CapturedByUserId = CurrentUserId };
        Apply(result, req.Status, req.FinishTimeSeconds, req.OverallPosition, req.CategoryPosition);
        db.Results.Add(result);
        await db.SaveChangesAsync();
        return StatusCode(Status201Created, ToResponse(result, enrolment.RaceNumber));
    }

    /// <summary>Corrects a previously captured result.</summary>
    [HttpPut("results/{id:int}"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<ResultResponse>(Status200OK)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> Update(int id, ResultRequest req)
    {
        var result = await LoadOwnedAsync(id);
        if (result is null) return Fail(404, "Result not found.");
        if (result.Enrolment.Category.Event.OrganiserId != CurrentUserId) return Fail(403, "You do not organise this event.");

        var error = Validate(req.Status, req.FinishTimeSeconds, req.OverallPosition, req.CategoryPosition);
        if (error is not null) return Fail(400, error);

        Apply(result, req.Status, req.FinishTimeSeconds, req.OverallPosition, req.CategoryPosition);
        await db.SaveChangesAsync();
        return Ok(ToResponse(result, result.Enrolment.RaceNumber));
    }

    /// <summary>Removes a result that was captured against the wrong race number.</summary>
    [HttpDelete("results/{id:int}"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(Status204NoContent)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await LoadOwnedAsync(id);
        if (result is null) return Fail(404, "Result not found.");
        if (result.Enrolment.Category.Event.OrganiserId != CurrentUserId) return Fail(403, "You do not organise this event.");

        db.Results.Remove(result);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Returns the public results board for an event, ordered by category and position. Supports a categoryId filter.</summary>
    [HttpGet("events/{eventId:int}/results"), AllowAnonymous]
    [ProducesResponseType<List<ResultsBoardRow>>(Status200OK)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> Board(int eventId, [FromQuery] int? categoryId)
    {
        if (!await db.Events.AnyAsync(e => e.EventId == eventId)) return Fail(404, "Event not found.");

        var query = db.Results.AsNoTracking().Where(r => r.Enrolment.Category.EventId == eventId);
        if (categoryId is not null) query = query.Where(r => r.Enrolment.CategoryId == categoryId);

        var rows = await query.OrderBy(r => r.Enrolment.Category.Name)
            .ThenBy(r => r.CategoryPosition == null).ThenBy(r => r.CategoryPosition)
            .Select(r => new ResultsBoardRow(r.Enrolment.RaceNumber,
                r.Enrolment.Participant.FirstName + " " + r.Enrolment.Participant.LastName,
                r.Enrolment.Category.Name, r.FinishTimeSeconds, r.OverallPosition, r.CategoryPosition, r.Status))
            .ToListAsync();
        return Ok(rows);
    }

    /// <summary>Returns the signed-in Participant's full race history, newest first, with pace and total finishers.</summary>
    [HttpGet("results/me"), Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType<List<MyResultRow>>(Status200OK)]
    [ProducesResponseType(Status403Forbidden)]
    public async Task<IActionResult> MyResults()
    {
        var me = CurrentUserId;
        var raw = await db.Results.AsNoTracking().Where(r => r.Enrolment.ParticipantId == me)
            .OrderByDescending(r => r.Enrolment.Category.Event.EventDate)
            .Select(r => new
            {
                r.ResultId,
                EventName = r.Enrolment.Category.Event.Name,
                EventDate = r.Enrolment.Category.Event.EventDate,
                CategoryName = r.Enrolment.Category.Name,
                DistanceKm = r.Enrolment.Category.DistanceKm,
                r.FinishTimeSeconds,
                r.OverallPosition,
                r.CategoryPosition,
                r.Status,
                TotalFinishers = db.Results.Count(x => x.Status == "Finished"
                    && x.Enrolment.Category.EventId == r.Enrolment.Category.EventId)
            })
            .ToListAsync();

        var rows = raw.Select(x => new MyResultRow(x.ResultId, x.EventName, x.EventDate, x.CategoryName, x.DistanceKm,
            x.FinishTimeSeconds, Pace(x.FinishTimeSeconds, x.DistanceKm), x.OverallPosition, x.CategoryPosition,
            x.TotalFinishers, x.Status)).ToList();
        return Ok(rows);
    }

    /// <summary>Returns aggregated personal statistics: events completed, total distance, personal bests and average pace.</summary>
    [HttpGet("results/me/stats"), Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType<StatsResponse>(Status200OK)]
    [ProducesResponseType(Status403Forbidden)]
    public async Task<IActionResult> MyStats()
    {
        var me = CurrentUserId;
        var entered = await db.Enrolments.CountAsync(n => n.ParticipantId == me && Lookups.ActiveEnrolment.Contains(n.Status));
        var finished = (await db.Results.AsNoTracking()
                .Where(r => r.Enrolment.ParticipantId == me && r.Status == "Finished" && r.FinishTimeSeconds != null)
                .Select(r => new { Distance = r.Enrolment.Category.DistanceKm, Time = r.FinishTimeSeconds, EventName = r.Enrolment.Category.Event.Name })
                .ToListAsync())
            .Select(r => new { r.Distance, Time = r.Time!.Value, r.EventName })
            .ToList();

        var totalDistance = finished.Sum(f => f.Distance);
        var bests = finished.GroupBy(f => f.Distance)
            .Select(g => g.OrderBy(f => f.Time).First())
            .OrderBy(f => f.Distance)
            .Select(f => new PersonalBest(f.Distance, f.Time, f.EventName))
            .ToList();
        int? averagePace = totalDistance > 0 ? (int)Math.Round(finished.Sum(f => f.Time) / (double)totalDistance) : null;

        return Ok(new StatsResponse(entered, finished.Count, totalDistance, bests, averagePace));
    }

    /// <summary>Uploads a whole field of results in one call, matched by race number. Rows that fail are reported and skipped.</summary>
    [HttpPost("events/{eventId:int}/results/bulk"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<BulkResultResponse>(Status200OK)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> Bulk(int eventId, List<BulkResultRow> rows)
    {
        var owner = await db.OrganiserOfEventAsync(eventId);
        if (owner is null) return Fail(404, "Event not found.");
        if (owner != CurrentUserId) return Fail(403, "You do not organise this event.");
        if (rows.Count == 0) return Fail(400, "No result rows were supplied.");

        var enrolments = await db.Enrolments.Include(n => n.Result).Where(n => n.EventId == eventId && n.RaceNumber != null).ToListAsync();
        var byRaceNumber = enrolments.GroupBy(n => n.RaceNumber!).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var errors = new List<BulkError>();
        var imported = 0;
        foreach (var row in rows)
        {
            var key = row.RaceNumber?.Trim() ?? string.Empty;
            if (!byRaceNumber.TryGetValue(key, out var enrolment)) { errors.Add(new BulkError(key, "Race number not found for this event.")); continue; }
            if (enrolment.Result is not null) { errors.Add(new BulkError(key, "A result already exists for this entry.")); continue; }

            var error = Validate(row.Status, row.FinishTimeSeconds, row.OverallPosition, row.CategoryPosition);
            if (error is not null) { errors.Add(new BulkError(key, error)); continue; }

            var result = new Result { Enrolment = enrolment, CapturedByUserId = owner.Value };
            Apply(result, row.Status, row.FinishTimeSeconds, row.OverallPosition, row.CategoryPosition);
            db.Results.Add(result);
            imported++;
        }

        await db.SaveChangesAsync();
        return Ok(new BulkResultResponse(imported, errors.Count, errors));
    }

    internal static ResultResponse ToResponse(Result r, string? raceNumber) =>
        new(r.ResultId, r.EnrolmentId, raceNumber, r.FinishTimeSeconds, r.OverallPosition, r.CategoryPosition, r.Status, r.CapturedAt);

    private static string? Validate(string status, int? time, int? overall, int? category)
    {
        if (!Lookups.ResultStatuses.Contains(status)) return $"Status must be one of: {string.Join(", ", Lookups.ResultStatuses)}.";
        if (time is <= 0) return "The finish time must be positive.";
        if (overall is <= 0 || category is <= 0) return "Positions must be positive.";
        if (status == "Finished" && time is null) return "A Finished result requires a finish time.";
        if (status != "Finished" && overall is not null) return "Only a Finished result can have an overall position.";
        return null;
    }

    private static void Apply(Result r, string status, int? time, int? overall, int? category)
    {
        r.Status = status;
        r.FinishTimeSeconds = time;
        r.OverallPosition = overall;
        r.CategoryPosition = category;
    }

    private static int? Pace(int? seconds, decimal distanceKm) =>
        seconds is null || distanceKm <= 0 ? null : (int)Math.Round(seconds.Value / (double)distanceKm);

    private Task<Result?> LoadOwnedAsync(int id) =>
        db.Results.Include(r => r.Enrolment).ThenInclude(n => n.Category).ThenInclude(c => c.Event).FirstOrDefaultAsync(r => r.ResultId == id);
}

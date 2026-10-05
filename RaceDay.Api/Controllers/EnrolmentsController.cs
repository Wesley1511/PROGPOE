using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace RaceDay.Api.Controllers;

[Route("api")]
[Authorize]
public class EnrolmentsController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>Enters the signed-in Participant into one category of an event and allocates a race number.</summary>
    [HttpPost("events/{eventId:int}/enrolments"), Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType<EnrolmentResponse>(Status201Created)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    [ProducesResponseType(Status409Conflict)]
    public async Task<IActionResult> Enrol(int eventId, EnrolRequest req)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Fail(404, "Event not found.");
        var category = await db.EventCategories.FirstOrDefaultAsync(c => c.CategoryId == req.CategoryId);
        if (category is null) return Fail(404, "Category not found.");
        if (category.EventId != eventId) return Fail(400, "That category does not belong to this event.");
        if (!RegistrationOpen(ev)) return Fail(409, "Registration is not open for this event.");

        var me = CurrentUserId;
        var ageError = await CheckAgeAsync(me, ev, category);
        if (ageError is not null) return Fail(400, ageError);

        var existing = await db.Enrolments.FirstOrDefaultAsync(n => n.EventId == eventId && n.ParticipantId == me);
        if (existing is not null && existing.Status != "Withdrawn") return Fail(409, "You are already entered for this event.");
        if (await IsFullAsync(category, existing?.EnrolmentId ?? 0)) return Fail(409, "This category is full.");

        var status = req.AmountPaid >= category.EntryFee ? "Confirmed" : "Pending";
        var enrolment = existing ?? new Enrolment { EventId = eventId, ParticipantId = me, RaceNumber = await NextRaceNumberAsync(eventId) };
        enrolment.CategoryId = category.CategoryId;
        enrolment.Status = status;
        enrolment.AmountPaid = req.AmountPaid;
        enrolment.PaymentReference = req.PaymentReference;
        enrolment.EnrolledOn = DateTime.UtcNow;
        if (existing is null) db.Enrolments.Add(enrolment);

        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Fail(409, "Your entry could not be recorded. Please try again."); }

        return CreatedAtAction(nameof(GetEnrolment), new { id = enrolment.EnrolmentId },
            new EnrolmentResponse(enrolment.EnrolmentId, enrolment.RaceNumber, category.Name, enrolment.Status, enrolment.AmountPaid));
    }

    /// <summary>Returns the signed-in Participant's own entries. Supports status and upcoming=true filters.</summary>
    [HttpGet("enrolments/me"), Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType<List<MyEnrolmentRow>>(Status200OK)]
    [ProducesResponseType(Status403Forbidden)]
    public async Task<IActionResult> Mine([FromQuery] string? status, [FromQuery] bool? upcoming)
    {
        var me = CurrentUserId;
        var query = db.Enrolments.AsNoTracking().Where(n => n.ParticipantId == me);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(n => n.Status == status);
        if (upcoming == true)
        {
            var today = Clock.Today;
            query = query.Where(n => n.Category.Event.EventDate >= today);
        }

        var rows = await query.OrderBy(n => n.Category.Event.EventDate)
            .Select(n => new MyEnrolmentRow(n.EnrolmentId, n.Category.EventId, n.Category.Event.Name, n.Category.Event.EventDate,
                n.Category.Name, n.Category.DistanceKm, n.RaceNumber, n.Status, n.Result != null))
            .ToListAsync();
        return Ok(rows);
    }

    /// <summary>Returns one entry in full (digital race entry confirmation). Visible to the participant and the event's organiser.</summary>
    [HttpGet("enrolments/{id:int}")]
    [ProducesResponseType<EnrolmentDetailResponse>(Status200OK)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> GetEnrolment(int id)
    {
        var n = await db.Enrolments.AsNoTracking().Include(x => x.Participant).Include(x => x.Result)
            .Include(x => x.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(x => x.EnrolmentId == id);
        if (n is null) return Fail(404, "Enrolment not found.");

        var me = CurrentUserId;
        if (n.ParticipantId != me && n.Category.Event.OrganiserId != me) return Fail(403, "You may not view this enrolment.");

        var ev = n.Category.Event;
        var c = n.Category;
        return Ok(new EnrolmentDetailResponse(n.EnrolmentId, n.RaceNumber, n.Status, n.EnrolledOn, n.AmountPaid, n.PaymentReference,
            n.ParticipantId, $"{n.Participant.FirstName} {n.Participant.LastName}",
            new EventLocationSummary(ev.EventId, ev.Name, ev.EventDate, ev.VenueName, ev.City, ev.Province),
            new CategorySummary(c.CategoryId, c.Name, c.DistanceKm, c.EntryFee, c.StartTime),
            n.Result is null ? null : ResultsController.ToResponse(n.Result, n.RaceNumber)));
    }

    /// <summary>Lets a Participant switch distance before registration closes.</summary>
    [HttpPut("enrolments/{id:int}/category"), Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType<EnrolmentResponse>(Status200OK)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    [ProducesResponseType(Status409Conflict)]
    public async Task<IActionResult> ChangeCategory(int id, ChangeCategoryRequest req)
    {
        var enrolment = await db.Enrolments.Include(x => x.Category).ThenInclude(c => c.Event).FirstOrDefaultAsync(x => x.EnrolmentId == id);
        if (enrolment is null) return Fail(404, "Enrolment not found.");
        if (enrolment.ParticipantId != CurrentUserId) return Fail(403, "This is not your entry.");

        var target = await db.EventCategories.FirstOrDefaultAsync(c => c.CategoryId == req.CategoryId);
        if (target is null) return Fail(404, "Category not found.");
        if (target.EventId != enrolment.EventId) return Fail(400, "That category belongs to a different event.");
        if (!Lookups.ActiveEnrolment.Contains(enrolment.Status)) return Fail(409, "Only active entries can be changed.");
        if (!RegistrationOpen(enrolment.Category.Event)) return Fail(409, "Registration has closed for this event.");

        if (target.CategoryId != enrolment.CategoryId)
        {
            var ageError = await CheckAgeAsync(enrolment.ParticipantId, enrolment.Category.Event, target);
            if (ageError is not null) return Fail(400, ageError);
            if (await IsFullAsync(target, id)) return Fail(409, "The target category is full.");

            enrolment.CategoryId = target.CategoryId;
            await db.SaveChangesAsync();
        }

        return Ok(new EnrolmentResponse(enrolment.EnrolmentId, enrolment.RaceNumber, target.Name, enrolment.Status, enrolment.AmountPaid));
    }

    /// <summary>Lets an Organiser confirm or cancel an entry, for example after verifying payment.</summary>
    [HttpPatch("enrolments/{id:int}/status"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<EnrolmentStatusResponse>(Status200OK)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> SetStatus(int id, StatusRequest req)
    {
        var enrolment = await db.Enrolments.Include(x => x.Category).ThenInclude(c => c.Event).FirstOrDefaultAsync(x => x.EnrolmentId == id);
        if (enrolment is null) return Fail(404, "Enrolment not found.");
        if (enrolment.Category.Event.OrganiserId != CurrentUserId) return Fail(403, "You do not organise this event.");
        if (!Lookups.OrganiserSettableEnrolment.Contains(req.Status))
            return Fail(400, $"Status must be one of: {string.Join(", ", Lookups.OrganiserSettableEnrolment)}.");
        if (enrolment.Status == "Withdrawn") return Fail(400, "A withdrawn entry cannot be changed.");

        enrolment.Status = req.Status;
        await db.SaveChangesAsync();
        return Ok(new EnrolmentStatusResponse(enrolment.EnrolmentId, enrolment.Status));
    }

    /// <summary>Withdraws the signed-in Participant from an event. The record is kept with status Withdrawn.</summary>
    [HttpDelete("enrolments/{id:int}"), Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(Status204NoContent)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    [ProducesResponseType(Status409Conflict)]
    public async Task<IActionResult> Withdraw(int id)
    {
        var enrolment = await db.Enrolments.Include(x => x.Category).ThenInclude(c => c.Event).FirstOrDefaultAsync(x => x.EnrolmentId == id);
        if (enrolment is null) return Fail(404, "Enrolment not found.");
        if (enrolment.ParticipantId != CurrentUserId) return Fail(403, "This is not your entry.");
        if (enrolment.Category.Event.EventDate < Clock.Today) return Fail(409, "The event has already taken place.");

        enrolment.Status = "Withdrawn";
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static bool RegistrationOpen(Event ev)
    {
        var today = Clock.Today;
        return ev.Status == "Published" && today >= ev.RegistrationOpensOn && today <= ev.RegistrationClosesOn;
    }

    private async Task<string?> CheckAgeAsync(int userId, Event ev, EventCategory category)
    {
        var dob = await db.ParticipantProfiles.AsNoTracking().Where(p => p.UserId == userId)
            .Select(p => (DateOnly?)p.DateOfBirth).FirstOrDefaultAsync();
        if (dob is null) return "Complete your participant profile before entering an event.";

        var age = ev.EventDate.Year - dob.Value.Year;
        if (dob.Value.AddYears(age) > ev.EventDate) age--;
        return age < category.MinimumAge ? $"The minimum age for this category is {category.MinimumAge}." : null;
    }

    private async Task<bool> IsFullAsync(EventCategory category, int excludeEnrolmentId)
    {
        if (category.MaxParticipants is not int max) return false;
        var taken = await db.Enrolments.CountAsync(n => n.CategoryId == category.CategoryId
            && Lookups.ActiveEnrolment.Contains(n.Status) && n.EnrolmentId != excludeEnrolmentId);
        return taken >= max;
    }

    private async Task<string> NextRaceNumberAsync(int eventId)
    {
        var numbers = await db.Enrolments.Where(n => n.EventId == eventId && n.RaceNumber != null).Select(n => n.RaceNumber!).ToListAsync();
        var highest = numbers
            .Select(r => int.TryParse(new string(r.Where(char.IsDigit).ToArray()), out var v) ? v : 0)
            .DefaultIfEmpty(0).Max();
        return $"R{highest + 1:D4}";
    }
}
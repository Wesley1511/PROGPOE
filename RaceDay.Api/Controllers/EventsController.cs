using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace RaceDay.Api.Controllers;

[Route("api/events")]
public class EventsController(RaceDayDbContext db, IWeatherService weather) : ApiControllerBase
{
    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        ["Draft"] = ["Published", "Cancelled"],
        ["Published"] = ["Cancelled", "Completed"],
        ["Cancelled"] = ["Published"],
        ["Completed"] = []
    };

    /// <summary>Lists published events for the public browse page, with optional filters and paging.</summary>
    [HttpGet, AllowAnonymous]
    [ProducesResponseType<PagedResponse<EventListItem>>(Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? province, [FromQuery] string? eventType,
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Events.AsNoTracking().Where(e => e.Status == "Published");
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.Name.Contains(term) || e.City.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(province)) query = query.Where(e => e.Province == province);
        if (!string.IsNullOrWhiteSpace(eventType)) query = query.Where(e => e.EventType == eventType);
        if (fromDate is not null) query = query.Where(e => e.EventDate >= fromDate);
        if (toDate is not null) query = query.Where(e => e.EventDate <= toDate);

        var total = await query.CountAsync();
        var items = await query.OrderBy(e => e.EventDate).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new EventListItem(e.EventId, e.Name, e.EventType, e.EventDate, e.City, e.Province, e.Status,
                e.Categories.Count,
                e.Media.Where(m => m.MediaType == "Banner").Select(m => m.BlobUri).FirstOrDefault()))
            .ToListAsync();
        return Ok(new PagedResponse<EventListItem>(items, page, pageSize, total));
    }

    /// <summary>Lists every event created by the calling Organiser, including drafts, with entry counts and revenue.</summary>
    [HttpGet("mine"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<List<MyEventResponse>>(Status200OK)]
    [ProducesResponseType(Status403Forbidden)]
    public async Task<IActionResult> Mine()
    {
        var me = CurrentUserId;
        var rows = await db.Events.AsNoTracking().Where(e => e.OrganiserId == me).OrderBy(e => e.EventDate)
            .Select(e => new MyEventResponse(e.EventId, e.Name, e.EventDate, e.Status,
                db.Enrolments.Count(n => n.Category.EventId == e.EventId && (n.Status == "Pending" || n.Status == "Confirmed")),
                db.Enrolments.Where(n => n.Category.EventId == e.EventId && n.Status == "Confirmed").Sum(n => n.AmountPaid)))
            .ToListAsync();
        return Ok(rows);
    }

    /// <summary>Returns one event with its categories, organiser name and remaining capacity per category.</summary>
    [HttpGet("{id:int}"), AllowAnonymous]
    [ProducesResponseType<EventDetailResponse>(Status200OK)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        var detail = await LoadDetailAsync(id);
        if (detail is null || (detail.Status == "Draft" && detail.OrganiserId != CurrentUserIdOrNull))
            return Fail(404, "Event not found.");
        return Ok(detail);
    }

    /// <summary>Creates a new event owned by the calling Organiser.</summary>
    [HttpPost, Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<EventDetailResponse>(Status201Created)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    public async Task<IActionResult> Create(EventRequest req)
    {
        var status = req.Status ?? "Published";
        if (status is not ("Draft" or "Published")) return Fail(400, "A new event must start as Draft or Published.");
        var error = Validate(req, null);
        if (error is not null) return Fail(400, error);

        var ev = new Event { OrganiserId = CurrentUserId, Status = status };
        Apply(ev, req);
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = ev.EventId }, await LoadDetailAsync(ev.EventId));
    }

    /// <summary>Updates an event the caller owns.</summary>
    [HttpPut("{id:int}"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<EventDetailResponse>(Status200OK)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> Update(int id, EventRequest req)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Fail(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(403, "You do not own this event.");

        var error = Validate(req, ev.EventDate);
        if (error is not null) return Fail(400, error);
        if (req.Status is not null && req.Status != ev.Status)
        {
            if (!CanMove(ev.Status, req.Status)) return Fail(400, $"An event cannot move from {ev.Status} to {req.Status}.");
            ev.Status = req.Status;
        }

        Apply(ev, req);
        await db.SaveChangesAsync();
        return Ok(await LoadDetailAsync(id));
    }

    /// <summary>Deletes an event that has no confirmed enrolments otherwise it must be cancelled instead.</summary>
    [HttpDelete("{id:int}"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(Status204NoContent)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    [ProducesResponseType(Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Fail(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(403, "You do not own this event.");
        if (await db.Enrolments.AnyAsync(n => n.Category.EventId == id && n.Status == "Confirmed"))
            return Fail(409, "This event has confirmed enrolments. Cancel it instead of deleting it.");

        db.Events.Remove(ev);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Moves an event between Draft, Published, Cancelled and Completed</summary>
    [HttpPatch("{id:int}/status"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<EventStatusResponse>(Status200OK)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> SetStatus(int id, StatusRequest req)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Fail(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(403, "You do not own this event.");
        if (!Lookups.EventStatuses.Contains(req.Status)) return Fail(400, "Unknown status.");
        if (req.Status != ev.Status)
        {
            if (!CanMove(ev.Status, req.Status)) return Fail(400, $"An event cannot move from {ev.Status} to {req.Status}.");
            ev.Status = req.Status;
            await db.SaveChangesAsync();
        }
        return Ok(new EventStatusResponse(ev.EventId, ev.Status));
    }

    /// <summary>Returns the race-day start list for one of the caller's events. Supports categoryId and status filters.</summary>
    [HttpGet("{id:int}/enrolments"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<List<EventEnrolmentRow>>(Status200OK)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> Enrolments(int id, [FromQuery] int? categoryId, [FromQuery] string? status)
    {
        var owner = await db.OrganiserOfEventAsync(id);
        if (owner is null) return Fail(404, "Event not found.");
        if (owner != CurrentUserId) return Fail(403, "You do not own this event.");

        var query = db.Enrolments.AsNoTracking().Where(n => n.Category.EventId == id);
        if (categoryId is not null) query = query.Where(n => n.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(n => n.Status == status);

        var rows = await query.OrderBy(n => n.RaceNumber)
            .Select(n => new EventEnrolmentRow(n.EnrolmentId, n.RaceNumber, n.Participant.FirstName + " " + n.Participant.LastName,
                n.Category.Name, n.Status, n.AmountPaid, n.Result != null))
            .ToListAsync();
        return Ok(rows);
    }

    /// <summary>Proxies the Open-Meteo weather API for the event's coordinates so participants can prepare for race day.</summary>
    [HttpGet("{id:int}/weather"), AllowAnonymous]
    [ProducesResponseType<WeatherResponse>(Status200OK)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status404NotFound)]
    [ProducesResponseType(Status503ServiceUnavailable)]
    public async Task<IActionResult> Weather(int id, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id, ct);
        if (ev is null || (ev.Status == "Draft" && ev.OrganiserId != CurrentUserIdOrNull)) return Fail(404, "Event not found.");
        if (ev.Latitude is null || ev.Longitude is null) return Fail(400, "This event has no coordinates.");

        try
        {
            return Ok(await weather.GetAsync((double)ev.Latitude.Value, (double)ev.Longitude.Value, ev.EventDate, ct));
        }
        catch (Exception)
        {
            return Fail(503, "The weather provider is unreachable.");
        }
    }

    private static bool CanMove(string from, string to) => Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    private static string? Validate(EventRequest r, DateOnly? currentDate)
    {
        if (!Lookups.EventTypes.Contains(r.EventType)) return $"EventType must be one of: {string.Join(", ", Lookups.EventTypes)}.";
        if (!Lookups.Provinces.Contains(r.Province)) return "Unknown province.";
        if (r.Status is not null && !Lookups.EventStatuses.Contains(r.Status)) return "Unknown status.";

        var date = r.EventDate!.Value;
        var opens = r.RegistrationOpensOn!.Value;
        var closes = r.RegistrationClosesOn!.Value;
        if (date < Clock.Today && date != currentDate) return "The event date cannot be in the past.";
        if (closes < opens) return "Registration cannot close before it opens.";
        if (closes > date) return "Registration must close on or before the event date.";
        return null;
    }

    private static void Apply(Event ev, EventRequest r)
    {
        ev.Name = r.Name.Trim();
        ev.Description = r.Description;
        ev.EventType = r.EventType;
        ev.EventDate = r.EventDate!.Value;
        ev.VenueName = r.VenueName.Trim();
        ev.City = r.City.Trim();
        ev.Province = r.Province;
        ev.Latitude = r.Latitude;
        ev.Longitude = r.Longitude;
        ev.RegistrationOpensOn = r.RegistrationOpensOn!.Value;
        ev.RegistrationClosesOn = r.RegistrationClosesOn!.Value;
    }

    private async Task<EventDetailResponse?> LoadDetailAsync(int id)
    {
        var e = await db.Events.AsNoTracking().Include(x => x.Organiser).FirstOrDefaultAsync(x => x.EventId == id);
        if (e is null) return null;

        var categories = await db.EventCategories.AsNoTracking().Where(c => c.EventId == id)
            .OrderBy(c => c.DistanceKm).Select(CategoryMap.ToResponse).ToListAsync();
        var banner = await db.EventMedia.AsNoTracking().Where(m => m.EventId == id && m.MediaType == "Banner")
            .Select(m => m.BlobUri).FirstOrDefaultAsync();

        return new EventDetailResponse(e.EventId, e.Name, e.Description, e.EventType, e.EventDate, e.VenueName, e.City,
            e.Province, e.Latitude, e.Longitude, e.RegistrationOpensOn, e.RegistrationClosesOn, e.Status, e.CreatedAt,
            e.OrganiserId, $"{e.Organiser.FirstName} {e.Organiser.LastName}", banner, categories);
    }
}

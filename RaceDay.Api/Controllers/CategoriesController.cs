using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace RaceDay.Api.Controllers;

[Route("api")]
public class CategoriesController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>Lists the distance options for an event with entry fee and start time and places remaining.</summary>
    [HttpGet("events/{eventId:int}/categories"), AllowAnonymous]
    [ProducesResponseType<List<CategoryResponse>>(Status200OK)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> List(int eventId)
    {
        if (!await db.Events.AnyAsync(e => e.EventId == eventId)) return Fail(404, "Event not found.");
        var rows = await db.EventCategories.AsNoTracking().Where(c => c.EventId == eventId)
            .OrderBy(c => c.DistanceKm).Select(CategoryMap.ToResponse).ToListAsync();
        return Ok(rows);
    }

    /// <summary>Adds a category to an event the caller owns.</summary>
    [HttpPost("events/{eventId:int}/categories"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<CategoryResponse>(Status201Created)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    [ProducesResponseType(Status409Conflict)]
    public async Task<IActionResult> Create(int eventId, CategoryRequest req)
    {
        var owner = await db.OrganiserOfEventAsync(eventId);
        if (owner is null) return Fail(404, "Event not found.");
        if (owner != CurrentUserId) return Fail(403, "You do not own this event.");

        var name = req.Name.Trim();
        if (await db.EventCategories.AnyAsync(c => c.EventId == eventId && c.Name == name))
            return Fail(409, "A category with that name already exists on this event.");

        var category = new EventCategory { EventId = eventId };
        Apply(category, req);
        db.EventCategories.Add(category);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = category.CategoryId }, await LoadAsync(category.CategoryId));
    }

    /// <summary>Returns a single category together with a summary of its event.</summary>
    [HttpGet("categories/{id:int}"), AllowAnonymous]
    [ProducesResponseType<CategoryDetailResponse>(Status200OK)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        var category = await LoadAsync(id);
        if (category is null) return Fail(404, "Category not found.");

        var ev = await db.Events.AsNoTracking().Where(e => e.EventId == category.EventId)
            .Select(e => new EventSummary(e.EventId, e.Name, e.EventDate, e.Status)).FirstAsync();
        return Ok(new CategoryDetailResponse(category, ev));
    }

    /// <summary>Updates a category the caller owns. The maximum cannot drop below the number already entered.</summary>
    [HttpPut("categories/{id:int}"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<CategoryResponse>(Status200OK)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    [ProducesResponseType(Status409Conflict)]
    public async Task<IActionResult> Update(int id, CategoryRequest req)
    {
        var category = await db.EventCategories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return Fail(404, "Category not found.");
        if (category.Event.OrganiserId != CurrentUserId) return Fail(403, "You do not own this event.");

        var name = req.Name.Trim();
        if (await db.EventCategories.AnyAsync(c => c.EventId == category.EventId && c.Name == name && c.CategoryId != id))
            return Fail(409, "A category with that name already exists on this event.");

        if (req.MaxParticipants is int max)
        {
            var entered = await db.Enrolments.CountAsync(n => n.CategoryId == id && Lookups.ActiveEnrolment.Contains(n.Status));
            if (max < entered) return Fail(400, $"MaxParticipants cannot be lower than the {entered} participants already entered.");
        }

        Apply(category, req);
        await db.SaveChangesAsync();
        return Ok(await LoadAsync(id));
    }

    /// <summary>Removes a category that nobody has entered yet.</summary>
    [HttpDelete("categories/{id:int}"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(Status204NoContent)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    [ProducesResponseType(Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await db.EventCategories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return Fail(404, "Category not found.");
        if (category.Event.OrganiserId != CurrentUserId) return Fail(403, "You do not own this event.");
        if (await db.Enrolments.AnyAsync(n => n.CategoryId == id)) return Fail(409, "This category already has enrolments.");

        db.EventCategories.Remove(category);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(EventCategory c, CategoryRequest r)
    {
        c.Name = r.Name.Trim();
        c.DistanceKm = r.DistanceKm;
        c.EntryFee = r.EntryFee;
        c.StartTime = r.StartTime!.Value;
        c.MaxParticipants = r.MaxParticipants;
        c.MinimumAge = r.MinimumAge;
        c.CutOffMinutes = r.CutOffMinutes;
    }

    private Task<CategoryResponse?> LoadAsync(int id) =>
        db.EventCategories.AsNoTracking().Where(c => c.CategoryId == id).Select(CategoryMap.ToResponse).FirstOrDefaultAsync();
}

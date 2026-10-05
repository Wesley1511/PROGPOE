using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace RaceDay.Api.Controllers;

[Route("api")]
public class WaypointsController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>Returns the ordered route points (start, water points, markers, finish) used to draw the route map.</summary>
    [HttpGet("categories/{categoryId:int}/waypoints"), AllowAnonymous]
    [ProducesResponseType<List<WaypointResponse>>(Status200OK)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> List(int categoryId)
    {
        if (!await db.EventCategories.AnyAsync(c => c.CategoryId == categoryId)) return Fail(404, "Category not found.");
        var rows = await db.RouteWaypoints.AsNoTracking().Where(w => w.CategoryId == categoryId)
            .OrderBy(w => w.SequenceNumber)
            .Select(w => new WaypointResponse(w.WaypointId, w.CategoryId, w.SequenceNumber, w.Name, w.WaypointType,
                w.DistanceMarkerKm, w.Latitude, w.Longitude))
            .ToListAsync();
        return Ok(rows);
    }

    /// <summary>Adds a waypoint to the route of a category the caller owns.</summary>
    [HttpPost("categories/{categoryId:int}/waypoints"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<WaypointResponse>(Status201Created)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    [ProducesResponseType(Status409Conflict)]
    public async Task<IActionResult> Create(int categoryId, WaypointRequest req)
    {
        var category = await db.EventCategories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null) return Fail(404, "Category not found.");
        if (category.Event.OrganiserId != CurrentUserId) return Fail(403, "You do not own this event.");
        if (!Lookups.WaypointTypes.Contains(req.WaypointType)) return Fail(400, $"WaypointType must be one of: {string.Join(", ", Lookups.WaypointTypes)}.");
        if (await db.RouteWaypoints.AnyAsync(w => w.CategoryId == categoryId && w.SequenceNumber == req.SequenceNumber))
            return Fail(409, "That sequence number is already used on this route.");

        var waypoint = new RouteWaypoint { CategoryId = categoryId };
        Apply(waypoint, req);
        db.RouteWaypoints.Add(waypoint);
        await db.SaveChangesAsync();
        return StatusCode(Status201Created, ToResponse(waypoint));
    }

    /// <summary>Updates a single waypoint on a route the caller owns.</summary>
    [HttpPut("waypoints/{id:int}"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<WaypointResponse>(Status200OK)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    [ProducesResponseType(Status409Conflict)]
    public async Task<IActionResult> Update(int id, WaypointRequest req)
    {
        var waypoint = await db.RouteWaypoints.Include(w => w.Category).ThenInclude(c => c.Event).FirstOrDefaultAsync(w => w.WaypointId == id);
        if (waypoint is null) return Fail(404, "Waypoint not found.");
        if (waypoint.Category.Event.OrganiserId != CurrentUserId) return Fail(403, "You do not own this event.");
        if (!Lookups.WaypointTypes.Contains(req.WaypointType)) return Fail(400, $"WaypointType must be one of: {string.Join(", ", Lookups.WaypointTypes)}.");
        if (await db.RouteWaypoints.AnyAsync(w => w.CategoryId == waypoint.CategoryId && w.SequenceNumber == req.SequenceNumber && w.WaypointId != id))
            return Fail(409, "That sequence number is already used on this route.");

        Apply(waypoint, req);
        await db.SaveChangesAsync();
        return Ok(ToResponse(waypoint));
    }

    /// <summary>Removes a waypoint from a route the caller owns.</summary>
    [HttpDelete("waypoints/{id:int}"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(Status204NoContent)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var waypoint = await db.RouteWaypoints.Include(w => w.Category).ThenInclude(c => c.Event).FirstOrDefaultAsync(w => w.WaypointId == id);
        if (waypoint is null) return Fail(404, "Waypoint not found.");
        if (waypoint.Category.Event.OrganiserId != CurrentUserId) return Fail(403, "You do not own this event.");

        db.RouteWaypoints.Remove(waypoint);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(RouteWaypoint w, WaypointRequest r)
    {
        w.SequenceNumber = r.SequenceNumber;
        w.Name = r.Name.Trim();
        w.WaypointType = r.WaypointType;
        w.DistanceMarkerKm = r.DistanceMarkerKm;
        w.Latitude = r.Latitude;
        w.Longitude = r.Longitude;
    }

    private static WaypointResponse ToResponse(RouteWaypoint w) =>
        new(w.WaypointId, w.CategoryId, w.SequenceNumber, w.Name, w.WaypointType, w.DistanceMarkerKm, w.Latitude, w.Longitude);
}
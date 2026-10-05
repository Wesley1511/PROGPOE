using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace RaceDay.Api.Controllers;

[Route("api/users")]
[Authorize]
public class UsersController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>Returns the signed-in user's account, plus the participant profile for Participants.</summary>
    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(Status200OK)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> Me()
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Role).Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        return user is null ? Fail(404, "User not found.") : Ok(ToResponse(user));
    }

    /// <summary>Updates the signed-in user's name and phone number.</summary>
    [HttpPut("me")]
    [ProducesResponseType<UserResponse>(Status200OK)]
    [ProducesResponseType(Status400BadRequest)]
    public async Task<IActionResult> UpdateMe(UpdateUserRequest req)
    {
        var user = await db.Users.Include(u => u.Role).Include(u => u.Profile).FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return Fail(404, "User not found.");
        user.FirstName = req.FirstName.Trim();
        user.LastName = req.LastName.Trim();
        user.PhoneNumber = req.PhoneNumber;
        await db.SaveChangesAsync();
        return Ok(ToResponse(user));
    }

    /// <summary>Creates or updates the participant-only profile (club, shirt size, emergency contact, medical notes).</summary>
    [HttpPut("me/profile"), Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType<ProfileResponse>(Status200OK)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status403Forbidden)]
    public async Task<IActionResult> UpdateProfile(ProfileRequest req)
    {
        var error = ProfileValidator.Validate(req.DateOfBirth, req.Gender, req.TShirtSize, req.EmergencyContactName, req.EmergencyContactPhone);
        if (error is not null) return Fail(400, error);

        var me = CurrentUserId;
        var profile = await db.ParticipantProfiles.FirstOrDefaultAsync(p => p.UserId == me);
        if (profile is null)
        {
            profile = new ParticipantProfile { UserId = me };
            db.ParticipantProfiles.Add(profile);
        }

        profile.DateOfBirth = req.DateOfBirth!.Value;
        profile.Gender = req.Gender!;
        profile.ClubName = req.ClubName;
        profile.TShirtSize = req.TShirtSize;
        profile.EmergencyContactName = req.EmergencyContactName;
        profile.EmergencyContactPhone = req.EmergencyContactPhone;
        profile.MedicalNotes = req.MedicalNotes;
        await db.SaveChangesAsync();
        return Ok(ToProfile(profile));
    }

    /// <summary>Lets an Organiser view contact and emergency details of a participant entered in one of their events.</summary>
    [HttpGet("{id:int}"), Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType<ContactResponse>(Status200OK)]
    [ProducesResponseType(Status403Forbidden)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> GetParticipant(int id)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Profile).FirstOrDefaultAsync(u => u.UserId == id);
        if (user is null) return Fail(404, "User not found.");

        var me = CurrentUserId;
        var related = await db.Enrolments.AnyAsync(e => e.ParticipantId == id && e.Category.Event.OrganiserId == me);
        if (!related) return Fail(403, "This user is not entered in any of your events.");

        return Ok(new ContactResponse(user.UserId, $"{user.FirstName} {user.LastName}", user.Email, user.PhoneNumber,
            user.Profile is null ? null : ToProfile(user.Profile)));
    }

    /// <summary>Soft-deletes the signed-in account (IsActive = false); historical results are retained.</summary>
    [HttpDelete("me")]
    [ProducesResponseType(Status204NoContent)]
    [ProducesResponseType(Status409Conflict)]
    public async Task<IActionResult> DeleteMe()
    {
        var me = CurrentUserId;
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == me);
        if (user is null) return Fail(404, "User not found.");

        var blocked = await db.Enrolments.AnyAsync(e => e.Category.Event.OrganiserId == me
            && e.Category.Event.Status == "Published" && e.Status == "Confirmed");
        if (blocked) return Fail(409, "You still have published events with confirmed entries.");

        user.IsActive = false;
        await db.SaveChangesAsync();
        HttpContext.Session.Clear();
        return NoContent();
    }

    private static ProfileResponse ToProfile(ParticipantProfile p) =>
        new(p.DateOfBirth, p.Gender, p.ClubName, p.TShirtSize, p.EmergencyContactName, p.EmergencyContactPhone, p.MedicalNotes);

    private static UserResponse ToResponse(AppUser u) =>
        new(u.UserId, u.Email, u.FirstName, u.LastName, u.PhoneNumber, u.Role.RoleName, u.CreatedAt,
            u.Profile is null ? null : ToProfile(u.Profile));
}

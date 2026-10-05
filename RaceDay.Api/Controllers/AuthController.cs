using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace RaceDay.Api.Controllers;

[Route("api/auth")]
public class AuthController(RaceDayDbContext db, TokenService tokens) : ApiControllerBase
{
    // Registers a new Organiser or Participant account. Participants must also supply profile details. Passwords are hashed with BCrypt.
    [HttpPost("register"), AllowAnonymous]
    [ProducesResponseType<RegisterResponse>(Status201Created)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var roleName = RoleNames.All.FirstOrDefault(r => r.Equals(req.Role, StringComparison.OrdinalIgnoreCase));
        if (roleName is null) return Fail(400, "Role must be Organiser or Participant.");
        if (req.Password != req.ConfirmPassword) return Fail(400, "Passwords do not match.");
        var passwordError = Passwords.Check(req.Password);
        if (passwordError is not null) return Fail(400, passwordError);

        if (roleName == RoleNames.Participant)
        {
            var profileError = ProfileValidator.Validate(req.DateOfBirth, req.Gender, req.TShirtSize, req.EmergencyContactName, req.EmergencyContactPhone);
            if (profileError is not null) return Fail(400, profileError);
        }

        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email)) return Fail(409, "That email address is already registered.");

        var user = new AppUser
        {
            Role = await db.Roles.FirstAsync(r => r.RoleName == roleName),
            Email = email,
            PasswordHash = Passwords.Hash(req.Password),
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            PhoneNumber = req.PhoneNumber
        };

        if (roleName == RoleNames.Participant)
        {
            user.Profile = new ParticipantProfile
            {
                DateOfBirth = req.DateOfBirth!.Value,
                Gender = req.Gender!,
                ClubName = req.ClubName,
                TShirtSize = req.TShirtSize,
                EmergencyContactName = req.EmergencyContactName!,
                EmergencyContactPhone = req.EmergencyContactPhone!,
                MedicalNotes = req.MedicalNotes
            };
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return StatusCode(Status201Created, new RegisterResponse(user.UserId, user.Email, roleName));
    }

    // Verifies credentials, starts a server-side session with user id and role and returns a JWT.
    [HttpPost("login"), AllowAnonymous]
    [ProducesResponseType<LoginResponse>(Status200OK)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !user.IsActive || !Passwords.Verify(req.Password, user.PasswordHash))
            return Fail(401, "Invalid email or password.");

        var fullName = $"{user.FirstName} {user.LastName}";
        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role.RoleName);
        HttpContext.Session.SetString(SessionKeys.Name, fullName);

        var (token, expiresAt) = tokens.Create(user);
        return Ok(new LoginResponse(token, expiresAt, user.UserId, fullName, user.Role.RoleName));
    }

    /// <summary>Ends the current server-side session.</summary>
    [HttpPost("logout"), Authorize]
    [ProducesResponseType(Status204NoContent)]
    [ProducesResponseType(Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }

    /// <summary>Changes the signed-in user's password after confirming the current one.</summary>
    [HttpPost("change-password"), Authorize]
    [ProducesResponseType(Status204NoContent)]
    [ProducesResponseType(Status400BadRequest)]
    [ProducesResponseType(Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null || !Passwords.Verify(req.CurrentPassword, user.PasswordHash))
            return Fail(401, "Current password is incorrect.");
        if (req.NewPassword != req.ConfirmNewPassword) return Fail(400, "New passwords do not match.");
        var error = Passwords.Check(req.NewPassword);
        if (error is not null) return Fail(400, error);

        user.PasswordHash = Passwords.Hash(req.NewPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Lists the available roles so the registration form can bind a dropdown.</summary>
    [HttpGet("roles"), AllowAnonymous]
    [ProducesResponseType<List<RoleResponse>>(Status200OK)]
    public async Task<IActionResult> Roles() =>
        Ok(await db.Roles.AsNoTracking().OrderBy(r => r.RoleId).Select(r => new RoleResponse(r.RoleId, r.RoleName, r.Description)).ToListAsync());
}
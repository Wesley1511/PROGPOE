using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    [Required] public string ConfirmPassword { get; set; } = string.Empty;
    [Required, MaxLength(60)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(60)] public string LastName { get; set; } = string.Empty;
    [MaxLength(20)] public string? PhoneNumber { get; set; }
    [Required] public string Role { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    [MaxLength(100)] public string? ClubName { get; set; }
    public string? TShirtSize { get; set; }
    [MaxLength(100)] public string? EmergencyContactName { get; set; }
    [MaxLength(20)] public string? EmergencyContactPhone { get; set; }
    [MaxLength(500)] public string? MedicalNotes { get; set; }
}

public record RegisterResponse(int UserId, string Email, string Role);

public class LoginRequest
{
    [Required] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public record LoginResponse(string Token, DateTime ExpiresAt, int UserId, string FullName, string Role);

public class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; set; } = string.Empty;
    [Required] public string NewPassword { get; set; } = string.Empty;
    [Required] public string ConfirmNewPassword { get; set; } = string.Empty;
}

public record RoleResponse(int RoleId, string RoleName, string? Description);

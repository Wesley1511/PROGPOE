using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class UpdateUserRequest
{
    [Required, MaxLength(60)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(60)] public string LastName { get; set; } = string.Empty;
    [MaxLength(20)] public string? PhoneNumber { get; set; }
}

public class ProfileRequest
{
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    [MaxLength(100)] public string? ClubName { get; set; }
    public string? TShirtSize { get; set; }
    [Required, MaxLength(100)] public string EmergencyContactName { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string EmergencyContactPhone { get; set; } = string.Empty;
    [MaxLength(500)] public string? MedicalNotes { get; set; }
}

public record ProfileResponse(DateOnly DateOfBirth, string Gender, string? ClubName, string? TShirtSize,
    string EmergencyContactName, string EmergencyContactPhone, string? MedicalNotes);

public record UserResponse(int UserId, string Email, string FirstName, string LastName, string? PhoneNumber,
    string Role, DateTime CreatedAt, ProfileResponse? Profile);

public record ContactResponse(int UserId, string FullName, string Email, string? PhoneNumber, ProfileResponse? Profile);

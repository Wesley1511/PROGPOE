namespace RaceDay.Api.Models;

public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}

public class AppUser
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ParticipantProfile? Profile { get; set; }
    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class ParticipantProfile
{
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string? ClubName { get; set; }
    public string? TShirtSize { get; set; }
    public string EmergencyContactName { get; set; } = string.Empty;
    public string EmergencyContactPhone { get; set; } = string.Empty;
    public string? MedicalNotes { get; set; }
}

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public AppUser Organiser { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateOnly RegistrationOpensOn { get; set; }
    public DateOnly RegistrationClosesOn { get; set; }
    public string Status { get; set; } = "Published";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<EventCategory> Categories { get; set; } = new List<EventCategory>();
    public ICollection<EventMedia> Media { get; set; } = new List<EventMedia>();
}

public class EventCategory
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public TimeOnly StartTime { get; set; }
    public int? MaxParticipants { get; set; }
    public int MinimumAge { get; set; }
    public int? CutOffMinutes { get; set; }
    public ICollection<RouteWaypoint> Waypoints { get; set; } = new List<RouteWaypoint>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class RouteWaypoint
{
    public int WaypointId { get; set; }
    public int CategoryId { get; set; }
    public EventCategory Category { get; set; } = null!;
    public int SequenceNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WaypointType { get; set; } = "Marker";
    public decimal? DistanceMarkerKm { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}

public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public int CategoryId { get; set; }
    public EventCategory Category { get; set; } = null!;
    public int ParticipantId { get; set; }
    public AppUser Participant { get; set; } = null!;
    public string? RaceNumber { get; set; }
    public DateTime EnrolledOn { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Confirmed";
    public decimal AmountPaid { get; set; }
    public string? PaymentReference { get; set; }
    public Result? Result { get; set; }
}

public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public Enrolment Enrolment { get; set; } = null!;
    public int CapturedByUserId { get; set; }
    public AppUser CapturedBy { get; set; } = null!;
    public int? FinishTimeSeconds { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    public string Status { get; set; } = "Finished";
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

public class EventMedia
{
    public int MediaId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public int UploadedByUserId { get; set; }
    public AppUser UploadedBy { get; set; } = null!;
    public string MediaType { get; set; } = "Gallery";
    public string FileName { get; set; } = string.Empty;
    public string BlobUri { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long? SizeInBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}


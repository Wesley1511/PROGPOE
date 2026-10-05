using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class EventRequest
{
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    [Required] public string EventType { get; set; } = string.Empty;
    [Required] public DateOnly? EventDate { get; set; }
    [Required, MaxLength(150)] public string VenueName { get; set; } = string.Empty;
    [Required, MaxLength(80)] public string City { get; set; } = string.Empty;
    [Required] public string Province { get; set; } = string.Empty;
    [Range(-90.0, 90.0)] public decimal? Latitude { get; set; }
    [Range(-180.0, 180.0)] public decimal? Longitude { get; set; }
    [Required] public DateOnly? RegistrationOpensOn { get; set; }
    [Required] public DateOnly? RegistrationClosesOn { get; set; }
    public string? Status { get; set; }
}

public class StatusRequest
{
    [Required] public string Status { get; set; } = string.Empty;
}

public record EventStatusResponse(int EventId, string Status);

public record EventListItem(int EventId, string Name, string EventType, DateOnly EventDate, string City,
    string Province, string Status, int CategoryCount, string? BannerUri);

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record EventDetailResponse(int EventId, string Name, string? Description, string EventType, DateOnly EventDate,
    string VenueName, string City, string Province, decimal? Latitude, decimal? Longitude,
    DateOnly RegistrationOpensOn, DateOnly RegistrationClosesOn, string Status, DateTime CreatedAt,
    int OrganiserId, string OrganiserName, string? BannerUri, IReadOnlyList<CategoryResponse> Categories);

public record MyEventResponse(int EventId, string Name, DateOnly EventDate, string Status, int EntryCount, decimal Revenue);

public record EventEnrolmentRow(int EnrolmentId, string? RaceNumber, string ParticipantName, string CategoryName,
    string Status, decimal AmountPaid, bool HasResult);

public record WeatherResponse(DateOnly EventDate, double? TemperatureC, double? FeelsLikeC, string Conditions,
    double? WindKph, int? ChanceOfRain, bool IsForecast);

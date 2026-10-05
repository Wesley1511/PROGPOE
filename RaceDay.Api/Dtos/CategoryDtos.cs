using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class CategoryRequest
{
    [Required, MaxLength(80)] public string Name { get; set; } = string.Empty;
    [Range(0.01, 1000)] public decimal DistanceKm { get; set; }
    [Range(0, 100000)] public decimal EntryFee { get; set; }
    [Required] public TimeOnly? StartTime { get; set; }
    [Range(1, int.MaxValue)] public int? MaxParticipants { get; set; }
    [Range(0, 100)] public int MinimumAge { get; set; }
    [Range(1, int.MaxValue)] public int? CutOffMinutes { get; set; }
}

public record CategoryResponse(int CategoryId, int EventId, string Name, decimal DistanceKm, decimal EntryFee,
    TimeOnly StartTime, int? MaxParticipants, int? PlacesRemaining, int MinimumAge, int? CutOffMinutes);

public record EventSummary(int EventId, string Name, DateOnly EventDate, string Status);

public record CategoryDetailResponse(CategoryResponse Category, EventSummary Event);

public static class CategoryMap
{
    public static readonly Expression<Func<EventCategory, CategoryResponse>> ToResponse = c => new CategoryResponse(
        c.CategoryId, c.EventId, c.Name, c.DistanceKm, c.EntryFee, c.StartTime, c.MaxParticipants,
        c.MaxParticipants == null
            ? (int?)null
            : c.MaxParticipants - c.Enrolments.Count(e => e.Status == "Pending" || e.Status == "Confirmed"),
        c.MinimumAge, c.CutOffMinutes);
}

public class WaypointRequest
{
    [Range(1, int.MaxValue)] public int SequenceNumber { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required] public string WaypointType { get; set; } = "Marker";
    [Range(0, 1000)] public decimal? DistanceMarkerKm { get; set; }
    [Range(-90.0, 90.0)] public decimal Latitude { get; set; }
    [Range(-180.0, 180.0)] public decimal Longitude { get; set; }
}

public record WaypointResponse(int WaypointId, int CategoryId, int SequenceNumber, string Name, string WaypointType,
    decimal? DistanceMarkerKm, decimal Latitude, decimal Longitude);
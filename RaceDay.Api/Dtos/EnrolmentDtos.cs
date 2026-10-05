using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class EnrolRequest
{
    [Required] public int? CategoryId { get; set; }
    [Range(0, 100000)] public decimal AmountPaid { get; set; }
    [MaxLength(50)] public string? PaymentReference { get; set; }
}

public class ChangeCategoryRequest
{
    [Required] public int? CategoryId { get; set; }
}

public record EnrolmentResponse(int EnrolmentId, string? RaceNumber, string CategoryName, string Status, decimal AmountPaid);

public record EnrolmentStatusResponse(int EnrolmentId, string Status);

public record MyEnrolmentRow(int EnrolmentId, int EventId, string EventName, DateOnly EventDate, string CategoryName,
    decimal DistanceKm, string? RaceNumber, string Status, bool HasResult);

public record CategorySummary(int CategoryId, string Name, decimal DistanceKm, decimal EntryFee, TimeOnly StartTime);

public record EventLocationSummary(int EventId, string Name, DateOnly EventDate, string VenueName, string City, string Province);

public record EnrolmentDetailResponse(int EnrolmentId, string? RaceNumber, string Status, DateTime EnrolledOn,
    decimal AmountPaid, string? PaymentReference, int ParticipantId, string ParticipantName,
    EventLocationSummary Event, CategorySummary Category, ResultResponse? Result);

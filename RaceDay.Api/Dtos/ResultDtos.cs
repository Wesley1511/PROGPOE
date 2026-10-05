using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class ResultRequest
{
    [Range(1, int.MaxValue)] public int? FinishTimeSeconds { get; set; }
    [Range(1, int.MaxValue)] public int? OverallPosition { get; set; }
    [Range(1, int.MaxValue)] public int? CategoryPosition { get; set; }
    [Required] public string Status { get; set; } = "Finished";
}

public record ResultResponse(int ResultId, int EnrolmentId, string? RaceNumber, int? FinishTimeSeconds,
    int? OverallPosition, int? CategoryPosition, string Status, DateTime CapturedAt);

public class BulkResultRow
{
    [Required] public string RaceNumber { get; set; } = string.Empty;
    public int? FinishTimeSeconds { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    public string Status { get; set; } = "Finished";
}

public record BulkError(string RaceNumber, string Reason);

public record BulkResultResponse(int Imported, int Skipped, IReadOnlyList<BulkError> Errors);

public record ResultsBoardRow(string? RaceNumber, string ParticipantName, string CategoryName, int? FinishTimeSeconds,
    int? OverallPosition, int? CategoryPosition, string Status);

public record MyResultRow(int ResultId, string EventName, DateOnly EventDate, string CategoryName, decimal DistanceKm,
    int? FinishTimeSeconds, int? PaceSecondsPerKm, int? OverallPosition, int? CategoryPosition, int TotalFinishers, string Status);

public record PersonalBest(decimal DistanceKm, int FinishTimeSeconds, string EventName);

public record StatsResponse(int EventsEntered, int EventsCompleted, decimal TotalDistanceKm,
    IReadOnlyList<PersonalBest> PersonalBests, int? AveragePaceSecondsPerKm);

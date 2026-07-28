namespace PulseBoard.Api.Models;

public sealed class DailyActivity
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserId { get; init; } = "local-dev";

    public string LocalDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow).ToString("O");

    public string TimeZoneId { get; init; } = "Europe/Vienna";

    public int Steps { get; init; }

    public int ActiveEnergyKcal { get; init; }

    public int? RestingEnergyKcal { get; init; }

    public int ExerciseMinutes { get; init; }

    public int? StandHours { get; init; }

    public int? StandMinutes { get; init; }

    public decimal? WalkingRunningDistanceKm { get; init; }

    public decimal? CyclingDistanceKm { get; init; }

    public int? FlightsClimbed { get; init; }

    public decimal? PhysicalEffortMet { get; init; }

    public int WorkoutCount { get; init; }

    public string Source { get; init; } = "Manual";

    public string? Notes { get; init; }

    public DateTimeOffset RecordedAtUtc { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

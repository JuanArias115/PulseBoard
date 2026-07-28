namespace PulseBoard.Api.Models;

public sealed class BodyMeasurement
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserId { get; init; } = "local-dev";

    public DateTimeOffset MeasuredAtUtc { get; init; }

    public string TimeZoneId { get; init; } = "Europe/Vienna";

    public decimal WeightKg { get; init; }

    public decimal? BodyFatPercentage { get; init; }

    public decimal? MusclePercentage { get; init; }

    public decimal? BodyWaterPercentage { get; init; }

    public decimal? BodyMassIndex { get; init; }

    public int? EstimatedCaloriesKcal { get; init; }

    public string Source { get; init; } = "SoehnleManual";

    public string? Notes { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

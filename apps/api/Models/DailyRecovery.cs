namespace PulseBoard.Api.Models;

public sealed class DailyRecovery
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserId { get; init; } = "local-dev";

    public string LocalDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow).ToString("O");

    public string TimeZoneId { get; init; } = "Europe/Vienna";

    public int? HeartRateBpm { get; init; }

    public int? RestingHeartRateBpm { get; init; }

    public decimal? HeartRateVariabilityMs { get; init; }

    public decimal? BloodOxygenPercentage { get; init; }

    public decimal? RespiratoryRateBreathsPerMinute { get; init; }

    public decimal? SleepHours { get; init; }

    public decimal? SleepScore { get; init; }

    public decimal? Vo2Max { get; init; }

    public int? WalkingHeartRateAverageBpm { get; init; }

    public string Source { get; init; } = "AppleHealth";

    public string? Notes { get; init; }

    public DateTimeOffset RecordedAtUtc { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

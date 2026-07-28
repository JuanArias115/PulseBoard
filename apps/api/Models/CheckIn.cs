namespace PulseBoard.Api.Models;

public sealed class CheckIn
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserId { get; init; } = "local-dev";

    public string LocalDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow).ToString("O");

    public string TimeZoneId { get; init; } = "Europe/Vienna";

    public decimal SleepHours { get; init; }

    public int SleepQuality { get; init; }

    public int Energy { get; init; }

    public int Mood { get; init; }

    public int Fatigue { get; init; }

    public int MuscleSoreness { get; init; }

    public int Hunger { get; init; }

    public int Stress { get; init; }

    public int Recovery { get; init; }

    public string? Note { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

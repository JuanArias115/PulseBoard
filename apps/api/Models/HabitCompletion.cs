namespace PulseBoard.Api.Models;

public sealed class HabitCompletion
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid HabitId { get; init; }

    public Habit? Habit { get; init; }

    public string UserId { get; init; } = "local-dev";

    public string LocalDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow).ToString("O");

    public string TimeZoneId { get; init; } = "Europe/Vienna";

    public decimal? Amount { get; init; }

    public string? Notes { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

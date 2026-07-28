namespace PulseBoard.Api.Models;

public sealed class Habit
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserId { get; init; } = "local-dev";

    public string Name { get; init; } = string.Empty;

    public string Category { get; init; } = "habit";

    public string Frequency { get; init; } = "daily";

    public decimal? TargetAmount { get; init; }

    public string? Unit { get; init; }

    public string? Notes { get; init; }

    public bool IsActive { get; init; } = true;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

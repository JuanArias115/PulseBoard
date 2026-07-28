namespace PulseBoard.Api.Models;

public sealed class DailyNutrition
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserId { get; init; } = "local-dev";

    public string LocalDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow).ToString("O");

    public string TimeZoneId { get; init; } = "Europe/Vienna";

    public int? CaloriesKcal { get; init; }

    public decimal? ProteinGrams { get; init; }

    public decimal? CarbohydrateGrams { get; init; }

    public decimal? FatGrams { get; init; }

    public decimal? FiberGrams { get; init; }

    public decimal? SugarGrams { get; init; }

    public decimal? WaterLiters { get; init; }

    public string Source { get; init; } = "AppleHealth";

    public string? Notes { get; init; }

    public DateTimeOffset RecordedAtUtc { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

namespace PulseBoard.Api.Models;

public sealed class Meal
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserId { get; init; } = "local-dev";

    public string LocalDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow).ToString("O");

    public string TimeZoneId { get; init; } = "Europe/Vienna";

    public string Name { get; init; } = string.Empty;

    public string MealType { get; init; } = "meal";

    public int CaloriesKcal { get; init; }

    public decimal ProteinGrams { get; init; }

    public decimal CarbohydrateGrams { get; init; }

    public decimal FatGrams { get; init; }

    public bool HasVegetables { get; init; }

    public bool IsFavorite { get; init; }

    public string? Notes { get; init; }

    public DateTimeOffset EatenAtUtc { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

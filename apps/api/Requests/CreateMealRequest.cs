using PulseBoard.Api.Models;

namespace PulseBoard.Api.Requests;

public sealed record CreateMealRequest(
    string LocalDate,
    DateTimeOffset EatenAt,
    string Name,
    string MealType,
    int CaloriesKcal,
    decimal ProteinGrams,
    decimal CarbohydrateGrams,
    decimal FatGrams,
    bool HasVegetables,
    bool IsFavorite,
    string? Notes)
{
    private static readonly string[] AllowedMealTypes = ["breakfast", "lunch", "dinner", "snack", "meal"];

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (!DateOnly.TryParse(LocalDate, out _))
        {
            errors[nameof(LocalDate)] = ["LocalDate must be a valid date."];
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors[nameof(Name)] = ["Name is required."];
        }

        if (!AllowedMealTypes.Contains(MealType))
        {
            errors[nameof(MealType)] = ["Meal type must be breakfast, lunch, dinner, snack or meal."];
        }

        if (CaloriesKcal is < 0 or > 6000)
        {
            errors[nameof(CaloriesKcal)] = ["Calories must be between 0 and 6000 kcal."];
        }

        AddMacroError(errors, nameof(ProteinGrams), ProteinGrams);
        AddMacroError(errors, nameof(CarbohydrateGrams), CarbohydrateGrams);
        AddMacroError(errors, nameof(FatGrams), FatGrams);

        return errors;
    }

    public Meal ToEntity() => new()
    {
        LocalDate = LocalDate,
        EatenAtUtc = EatenAt.ToUniversalTime(),
        Name = Name.Trim(),
        MealType = MealType.Trim(),
        CaloriesKcal = CaloriesKcal,
        ProteinGrams = ProteinGrams,
        CarbohydrateGrams = CarbohydrateGrams,
        FatGrams = FatGrams,
        HasVegetables = HasVegetables,
        IsFavorite = IsFavorite,
        Notes = Notes?.Trim()
    };

    private static void AddMacroError(Dictionary<string, string[]> errors, string field, decimal value)
    {
        if (value is < 0 or > 1000)
        {
            errors[field] = [$"{field} must be between 0 and 1000 grams."];
        }
    }
}

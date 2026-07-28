using PulseBoard.Api.Models;

namespace PulseBoard.Api.Requests;

public sealed record CreateDailyNutritionRequest(
    string LocalDate,
    int? CaloriesKcal,
    decimal? ProteinGrams,
    decimal? CarbohydrateGrams,
    decimal? FatGrams,
    decimal? FiberGrams,
    decimal? SugarGrams,
    decimal? WaterLiters,
    string? Notes)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (!DateOnly.TryParse(LocalDate, out _))
        {
            errors[nameof(LocalDate)] = ["LocalDate must be a valid date."];
        }

        if (CaloriesKcal is null
            && ProteinGrams is null
            && CarbohydrateGrams is null
            && FatGrams is null
            && FiberGrams is null
            && SugarGrams is null
            && WaterLiters is null)
        {
            errors["payload"] = ["At least one nutrition value is required."];
        }

        AddOptionalIntRangeError(errors, nameof(CaloriesKcal), CaloriesKcal, 0, 20_000);
        AddOptionalRangeError(errors, nameof(ProteinGrams), ProteinGrams, 0, 1_000);
        AddOptionalRangeError(errors, nameof(CarbohydrateGrams), CarbohydrateGrams, 0, 2_000);
        AddOptionalRangeError(errors, nameof(FatGrams), FatGrams, 0, 1_000);
        AddOptionalRangeError(errors, nameof(FiberGrams), FiberGrams, 0, 300);
        AddOptionalRangeError(errors, nameof(SugarGrams), SugarGrams, 0, 1_000);
        AddOptionalRangeError(errors, nameof(WaterLiters), WaterLiters, 0, 20);

        return errors;
    }

    public DailyNutrition ToEntity(string source = "AppleHealth") => new()
    {
        LocalDate = LocalDate,
        CaloriesKcal = CaloriesKcal,
        ProteinGrams = ProteinGrams,
        CarbohydrateGrams = CarbohydrateGrams,
        FatGrams = FatGrams,
        FiberGrams = FiberGrams,
        SugarGrams = SugarGrams,
        WaterLiters = WaterLiters,
        Source = source,
        Notes = Notes?.Trim(),
        RecordedAtUtc = DateTimeOffset.UtcNow
    };

    private static void AddOptionalRangeError(Dictionary<string, string[]> errors, string field, decimal? value, decimal min, decimal max)
    {
        if (value is not null && (value < min || value > max))
        {
            errors[field] = [$"{field} must be between {min} and {max}."];
        }
    }

    private static void AddOptionalIntRangeError(Dictionary<string, string[]> errors, string field, int? value, int min, int max)
    {
        if (value is not null && (value < min || value > max))
        {
            errors[field] = [$"{field} must be between {min} and {max}."];
        }
    }
}

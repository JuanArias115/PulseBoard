using PulseBoard.Api.Models;

namespace PulseBoard.Api.Requests;

public sealed record CreateBodyMeasurementRequest(
    DateTimeOffset MeasuredAt,
    decimal WeightKg,
    decimal? BodyFatPercentage,
    decimal? MusclePercentage,
    decimal? BodyWaterPercentage,
    decimal? BodyMassIndex,
    int? EstimatedCaloriesKcal,
    string? Notes)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        AddRangeError(errors, nameof(WeightKg), WeightKg, 20, 300);
        AddOptionalRangeError(errors, nameof(BodyFatPercentage), BodyFatPercentage, 1, 80);
        AddOptionalRangeError(errors, nameof(MusclePercentage), MusclePercentage, 1, 90);
        AddOptionalRangeError(errors, nameof(BodyWaterPercentage), BodyWaterPercentage, 20, 80);
        AddOptionalRangeError(errors, nameof(BodyMassIndex), BodyMassIndex, 10, 80);

        if (EstimatedCaloriesKcal is < 500 or > 8000)
        {
            errors[nameof(EstimatedCaloriesKcal)] = ["Calories must be between 500 and 8000 kcal."];
        }

        return errors;
    }

    public BodyMeasurement ToEntity() => new()
    {
        MeasuredAtUtc = MeasuredAt.ToUniversalTime(),
        WeightKg = WeightKg,
        BodyFatPercentage = BodyFatPercentage,
        MusclePercentage = MusclePercentage,
        BodyWaterPercentage = BodyWaterPercentage,
        BodyMassIndex = BodyMassIndex,
        EstimatedCaloriesKcal = EstimatedCaloriesKcal,
        Notes = Notes
    };

    private static void AddRangeError(Dictionary<string, string[]> errors, string field, decimal value, decimal min, decimal max)
    {
        if (value < min || value > max)
        {
            errors[field] = [$"{field} must be between {min} and {max}."];
        }
    }

    private static void AddOptionalRangeError(Dictionary<string, string[]> errors, string field, decimal? value, decimal min, decimal max)
    {
        if (value is not null)
        {
            AddRangeError(errors, field, value.Value, min, max);
        }
    }
}

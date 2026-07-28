using PulseBoard.Api.Models;

namespace PulseBoard.Api.Requests;

public sealed record CreateDailyActivityRequest(
    string LocalDate,
    int Steps,
    int ActiveEnergyKcal,
    int ExerciseMinutes,
    decimal? WalkingRunningDistanceKm,
    decimal? CyclingDistanceKm,
    int WorkoutCount,
    string? Notes)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (!DateOnly.TryParse(LocalDate, out _))
        {
            errors[nameof(LocalDate)] = ["LocalDate must be a valid date."];
        }

        AddRangeError(errors, nameof(Steps), Steps, 0, 100_000);
        AddRangeError(errors, nameof(ActiveEnergyKcal), ActiveEnergyKcal, 0, 10_000);
        AddRangeError(errors, nameof(ExerciseMinutes), ExerciseMinutes, 0, 1_440);
        AddRangeError(errors, nameof(WorkoutCount), WorkoutCount, 0, 30);
        AddOptionalRangeError(errors, nameof(WalkingRunningDistanceKm), WalkingRunningDistanceKm, 0, 300);
        AddOptionalRangeError(errors, nameof(CyclingDistanceKm), CyclingDistanceKm, 0, 500);

        return errors;
    }

    public DailyActivity ToEntity(string source = "Manual") => new()
    {
        LocalDate = LocalDate,
        Steps = Steps,
        ActiveEnergyKcal = ActiveEnergyKcal,
        ExerciseMinutes = ExerciseMinutes,
        WalkingRunningDistanceKm = WalkingRunningDistanceKm,
        CyclingDistanceKm = CyclingDistanceKm,
        WorkoutCount = WorkoutCount,
        Source = source,
        Notes = Notes?.Trim(),
        RecordedAtUtc = DateTimeOffset.UtcNow
    };

    private static void AddRangeError(Dictionary<string, string[]> errors, string field, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            errors[field] = [$"{field} must be between {min} and {max}."];
        }
    }

    private static void AddOptionalRangeError(Dictionary<string, string[]> errors, string field, decimal? value, decimal min, decimal max)
    {
        if (value is not null && (value < min || value > max))
        {
            errors[field] = [$"{field} must be between {min} and {max}."];
        }
    }
}

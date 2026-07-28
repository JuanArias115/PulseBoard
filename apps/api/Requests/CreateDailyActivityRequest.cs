using PulseBoard.Api.Models;

namespace PulseBoard.Api.Requests;

public sealed record CreateDailyActivityRequest(
    string LocalDate,
    int Steps,
    int ActiveEnergyKcal,
    int? RestingEnergyKcal,
    int ExerciseMinutes,
    int? StandHours,
    int? StandMinutes,
    decimal? WalkingRunningDistanceKm,
    decimal? CyclingDistanceKm,
    int? FlightsClimbed,
    decimal? PhysicalEffortMet,
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
        AddOptionalIntRangeError(errors, nameof(RestingEnergyKcal), RestingEnergyKcal, 0, 10_000);
        AddOptionalIntRangeError(errors, nameof(StandHours), StandHours, 0, 24);
        AddOptionalIntRangeError(errors, nameof(StandMinutes), StandMinutes, 0, 1_440);
        AddOptionalIntRangeError(errors, nameof(FlightsClimbed), FlightsClimbed, 0, 2_000);
        AddOptionalRangeError(errors, nameof(WalkingRunningDistanceKm), WalkingRunningDistanceKm, 0, 300);
        AddOptionalRangeError(errors, nameof(CyclingDistanceKm), CyclingDistanceKm, 0, 500);
        AddOptionalRangeError(errors, nameof(PhysicalEffortMet), PhysicalEffortMet, 0, 25);

        return errors;
    }

    public DailyActivity ToEntity(string source = "Manual") => new()
    {
        LocalDate = LocalDate,
        Steps = Steps,
        ActiveEnergyKcal = ActiveEnergyKcal,
        RestingEnergyKcal = RestingEnergyKcal,
        ExerciseMinutes = ExerciseMinutes,
        StandHours = StandHours,
        StandMinutes = StandMinutes,
        WalkingRunningDistanceKm = WalkingRunningDistanceKm,
        CyclingDistanceKm = CyclingDistanceKm,
        FlightsClimbed = FlightsClimbed,
        PhysicalEffortMet = PhysicalEffortMet,
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

    private static void AddOptionalIntRangeError(Dictionary<string, string[]> errors, string field, int? value, int min, int max)
    {
        if (value is not null && (value < min || value > max))
        {
            errors[field] = [$"{field} must be between {min} and {max}."];
        }
    }
}

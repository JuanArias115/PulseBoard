namespace PulseBoard.Api.Requests;

public sealed record CreateAppleHealthDailyActivityRequest(
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
    public CreateDailyActivityRequest ToDailyActivityRequest() => new(
        LocalDate,
        Steps,
        ActiveEnergyKcal,
        RestingEnergyKcal,
        ExerciseMinutes,
        StandHours,
        StandMinutes,
        WalkingRunningDistanceKm,
        CyclingDistanceKm,
        FlightsClimbed,
        PhysicalEffortMet,
        WorkoutCount,
        Notes);
}

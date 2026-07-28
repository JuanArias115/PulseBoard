namespace PulseBoard.Api.Requests;

public sealed record CreateAppleHealthDailyActivityRequest(
    string LocalDate,
    int Steps,
    int ActiveEnergyKcal,
    int ExerciseMinutes,
    decimal? WalkingRunningDistanceKm,
    decimal? CyclingDistanceKm,
    int WorkoutCount,
    string? Notes)
{
    public CreateDailyActivityRequest ToDailyActivityRequest() => new(
        LocalDate,
        Steps,
        ActiveEnergyKcal,
        ExerciseMinutes,
        WalkingRunningDistanceKm,
        CyclingDistanceKm,
        WorkoutCount,
        Notes);
}

namespace PulseBoard.Api.Requests;

public sealed record CreateAppleHealthBodyMeasurementRequest(
    DateTimeOffset MeasuredAt,
    decimal WeightKg,
    decimal? BodyFatPercentage,
    decimal? MusclePercentage,
    decimal? BodyWaterPercentage,
    decimal? BodyMassIndex,
    int? EstimatedCaloriesKcal,
    string? Notes)
{
    public CreateBodyMeasurementRequest ToBodyMeasurementRequest() => new(
        MeasuredAt,
        WeightKg,
        BodyFatPercentage,
        MusclePercentage,
        BodyWaterPercentage,
        BodyMassIndex,
        EstimatedCaloriesKcal,
        Notes);
}

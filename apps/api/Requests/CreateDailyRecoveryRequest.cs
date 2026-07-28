using PulseBoard.Api.Models;

namespace PulseBoard.Api.Requests;

public sealed record CreateDailyRecoveryRequest(
    string LocalDate,
    int? HeartRateBpm,
    int? RestingHeartRateBpm,
    decimal? HeartRateVariabilityMs,
    decimal? BloodOxygenPercentage,
    decimal? RespiratoryRateBreathsPerMinute,
    decimal? SleepHours,
    decimal? SleepScore,
    decimal? Vo2Max,
    int? WalkingHeartRateAverageBpm,
    string? Notes)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (!DateOnly.TryParse(LocalDate, out _))
        {
            errors[nameof(LocalDate)] = ["LocalDate must be a valid date."];
        }

        if (HeartRateBpm is null
            && RestingHeartRateBpm is null
            && HeartRateVariabilityMs is null
            && BloodOxygenPercentage is null
            && RespiratoryRateBreathsPerMinute is null
            && SleepHours is null
            && SleepScore is null
            && Vo2Max is null
            && WalkingHeartRateAverageBpm is null)
        {
            errors["payload"] = ["At least one recovery value is required."];
        }

        AddOptionalIntRangeError(errors, nameof(HeartRateBpm), HeartRateBpm, 20, 240);
        AddOptionalIntRangeError(errors, nameof(RestingHeartRateBpm), RestingHeartRateBpm, 20, 160);
        AddOptionalIntRangeError(errors, nameof(WalkingHeartRateAverageBpm), WalkingHeartRateAverageBpm, 20, 220);
        AddOptionalRangeError(errors, nameof(HeartRateVariabilityMs), HeartRateVariabilityMs, 0, 400);
        AddOptionalRangeError(errors, nameof(BloodOxygenPercentage), BloodOxygenPercentage, 50, 100);
        AddOptionalRangeError(errors, nameof(RespiratoryRateBreathsPerMinute), RespiratoryRateBreathsPerMinute, 4, 60);
        AddOptionalRangeError(errors, nameof(SleepHours), SleepHours, 0, 24);
        AddOptionalRangeError(errors, nameof(SleepScore), SleepScore, 0, 100);
        AddOptionalRangeError(errors, nameof(Vo2Max), Vo2Max, 0, 100);

        return errors;
    }

    public DailyRecovery ToEntity(string source = "AppleHealth") => new()
    {
        LocalDate = LocalDate,
        HeartRateBpm = HeartRateBpm,
        RestingHeartRateBpm = RestingHeartRateBpm,
        HeartRateVariabilityMs = HeartRateVariabilityMs,
        BloodOxygenPercentage = BloodOxygenPercentage,
        RespiratoryRateBreathsPerMinute = RespiratoryRateBreathsPerMinute,
        SleepHours = SleepHours,
        SleepScore = SleepScore,
        Vo2Max = Vo2Max,
        WalkingHeartRateAverageBpm = WalkingHeartRateAverageBpm,
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

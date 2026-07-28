using PulseBoard.Api.Models;

namespace PulseBoard.Api.Requests;

public sealed record CreateCheckInRequest(
    string LocalDate,
    decimal SleepHours,
    int SleepQuality,
    int Energy,
    int Mood,
    int Fatigue,
    int MuscleSoreness,
    int Hunger,
    int Stress,
    int Recovery,
    string? Note)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (!DateOnly.TryParse(LocalDate, out _))
        {
            errors[nameof(LocalDate)] = ["LocalDate must be a valid date."];
        }

        if (SleepHours is < 0 or > 24)
        {
            errors[nameof(SleepHours)] = ["Sleep hours must be between 0 and 24."];
        }

        AddScaleError(errors, nameof(SleepQuality), SleepQuality);
        AddScaleError(errors, nameof(Energy), Energy);
        AddScaleError(errors, nameof(Mood), Mood);
        AddScaleError(errors, nameof(Fatigue), Fatigue);
        AddScaleError(errors, nameof(MuscleSoreness), MuscleSoreness);
        AddScaleError(errors, nameof(Hunger), Hunger);
        AddScaleError(errors, nameof(Stress), Stress);
        AddScaleError(errors, nameof(Recovery), Recovery);

        return errors;
    }

    public CheckIn ToEntity() => new()
    {
        LocalDate = LocalDate,
        SleepHours = SleepHours,
        SleepQuality = SleepQuality,
        Energy = Energy,
        Mood = Mood,
        Fatigue = Fatigue,
        MuscleSoreness = MuscleSoreness,
        Hunger = Hunger,
        Stress = Stress,
        Recovery = Recovery,
        Note = Note
    };

    private static void AddScaleError(Dictionary<string, string[]> errors, string field, int value)
    {
        if (value is < 1 or > 5)
        {
            errors[field] = [$"{field} must be between 1 and 5."];
        }
    }
}

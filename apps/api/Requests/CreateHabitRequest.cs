using PulseBoard.Api.Models;

namespace PulseBoard.Api.Requests;

public sealed record CreateHabitRequest(
    string Name,
    string Category,
    string Frequency,
    decimal? TargetAmount,
    string? Unit,
    string? Notes)
{
    private static readonly string[] AllowedFrequencies = ["daily", "weekly"];

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors[nameof(Name)] = ["Name is required."];
        }

        if (string.IsNullOrWhiteSpace(Category))
        {
            errors[nameof(Category)] = ["Category is required."];
        }

        if (!AllowedFrequencies.Contains(Frequency))
        {
            errors[nameof(Frequency)] = ["Frequency must be daily or weekly."];
        }

        if (TargetAmount is < 0)
        {
            errors[nameof(TargetAmount)] = ["Target amount cannot be negative."];
        }

        return errors;
    }

    public Habit ToEntity() => new()
    {
        Name = Name.Trim(),
        Category = Category.Trim(),
        Frequency = Frequency,
        TargetAmount = TargetAmount,
        Unit = Unit?.Trim(),
        Notes = Notes?.Trim()
    };
}

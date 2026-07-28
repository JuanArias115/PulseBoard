using PulseBoard.Api.Models;

namespace PulseBoard.Api.Requests;

public sealed record CreateHabitCompletionRequest(
    string LocalDate,
    decimal? Amount,
    string? Notes)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (!DateOnly.TryParse(LocalDate, out _))
        {
            errors[nameof(LocalDate)] = ["LocalDate must be a valid date."];
        }

        if (Amount is < 0)
        {
            errors[nameof(Amount)] = ["Amount cannot be negative."];
        }

        return errors;
    }

    public HabitCompletion ToEntity(Guid habitId) => new()
    {
        HabitId = habitId,
        LocalDate = LocalDate,
        Amount = Amount,
        Notes = Notes?.Trim()
    };
}

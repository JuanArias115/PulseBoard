namespace PulseBoard.Api;

public sealed class PulseBoardOptions
{
    public const string SectionName = "PulseBoard";

    public string DefaultLanguage { get; init; } = "es";

    public string[] SupportedLanguages { get; init; } = ["es", "en"];

    public string TimeZoneId { get; init; } = "Europe/Vienna";
}

namespace RpaDevAssistant.Core.Analysis.Profiles;

public sealed record UiPathNamingConventionConfiguration
{
    public string? Pattern { get; init; }

    public string? RequiredPrefix { get; init; }

    public string? InPrefix { get; init; }

    public string? OutPrefix { get; init; }

    public string? InOutPrefix { get; init; }
}

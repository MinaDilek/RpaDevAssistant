namespace RpaDevAssistant.Core.Analysis.CustomRules;

public sealed record UiPathCustomRuleOptions
{
    public string? ConfigFilePath { get; init; }

    public int NoiseWarningThreshold { get; init; } = 500;
}

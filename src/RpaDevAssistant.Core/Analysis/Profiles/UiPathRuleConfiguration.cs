namespace RpaDevAssistant.Core.Analysis.Profiles;

public sealed record UiPathRuleConfiguration
{
    public required string RuleId { get; init; }

    public bool Enabled { get; init; } = true;

    public RuleSeverity? SeverityOverride { get; init; }

    public double Weight { get; init; }

    public double MaxPenalty { get; init; }

    public string? Description { get; init; }

    public UiPathNamingConventionConfiguration? NamingConvention { get; init; }
}

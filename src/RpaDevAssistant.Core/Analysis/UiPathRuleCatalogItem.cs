namespace RpaDevAssistant.Core.Analysis;

using RpaDevAssistant.Core.Analysis.RuleCatalog;

public sealed record UiPathRuleCatalogItem
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required RuleCategory Category { get; init; }

    public required RuleSeverity DefaultSeverity { get; init; }

    public string? Recommendation { get; init; }

    public bool HasFixSuggestion { get; init; }

    public bool CanAutoApply { get; init; }

    public UiPathRuleScope Scope { get; init; }

    public bool EnabledByDefault { get; init; }

    public bool IsBuiltIn { get; init; }

    public bool IsCustom => !IsBuiltIn;

    public string Source => IsBuiltIn ? "BuiltIn" : "Custom";

    public bool SupportsAggregation { get; init; }

    public double DefaultWeight { get; init; }

    public double DefaultMaxPenalty { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}

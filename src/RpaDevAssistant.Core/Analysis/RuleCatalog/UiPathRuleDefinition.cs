namespace RpaDevAssistant.Core.Analysis.RuleCatalog;

public sealed record UiPathRuleDefinition
{
    public required string Id { get; init; }

    public required string NameKey { get; init; }

    public required string DescriptionKey { get; init; }

    public required string RecommendationKey { get; init; }

    public required RuleCategory Category { get; init; }

    public required RuleSeverity DefaultSeverity { get; init; }

    public double DefaultWeight { get; init; }

    public double DefaultMaxPenalty { get; init; }

    public UiPathRuleScope Scope { get; init; }

    public bool EnabledByDefault { get; init; } = true;

    public bool IsBuiltIn { get; init; } = true;

    public bool IsTemplate { get; init; }

    public string? TemplateId { get; init; }

    public string? TemplateSource { get; init; }

    public bool SupportsFixSuggestion { get; init; }

    public bool SupportsAggregation { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<string> ApplicableProjectTypes { get; init; } = [];

    public string? CompatibilityNotesKey { get; init; }

    public string? CustomName { get; init; }

    public string? CustomDescription { get; init; }

    public string? CustomRecommendation { get; init; }
}

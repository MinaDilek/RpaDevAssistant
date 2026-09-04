using RpaDevAssistant.Core.Analysis.RuleCatalog;

namespace RpaDevAssistant.Core.Analysis.CustomRules;

public sealed record UiPathCustomRuleDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? NameTr { get; init; }

    public string? NameEn { get; init; }

    public string? Description { get; init; }

    public string? DescriptionTr { get; init; }

    public string? DescriptionEn { get; init; }

    public string? Recommendation { get; init; }

    public string? RecommendationTr { get; init; }

    public string? RecommendationEn { get; init; }

    public RuleCategory Category { get; init; } = RuleCategory.Maintainability;

    public RuleSeverity Severity { get; init; } = RuleSeverity.Suggestion;

    public UiPathRuleScope Scope { get; init; } = UiPathRuleScope.Activity;

    public bool Enabled { get; init; } = true;

    public double Weight { get; init; } = 1;

    public double MaxPenalty { get; init; } = 10;

    public IReadOnlyList<UiPathRuleCondition> Conditions { get; init; } = [];

    public UiPathCustomRuleMatchMode MatchMode { get; init; } = UiPathCustomRuleMatchMode.All;
}

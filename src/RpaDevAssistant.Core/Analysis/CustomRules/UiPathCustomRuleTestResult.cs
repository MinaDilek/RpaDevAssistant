namespace RpaDevAssistant.Core.Analysis.CustomRules;

public sealed record UiPathCustomRuleTestResult
{
    public required string RuleId { get; init; }

    public int MatchedWorkflowCount { get; init; }

    public int MatchedActivityCount { get; init; }

    public int EstimatedFindingCount { get; init; }

    public bool HasNoiseWarning { get; init; }

    public string? NoiseWarning { get; init; }

    public IReadOnlyList<string> MatchedWorkflows { get; init; } = [];

    public IReadOnlyList<UiPathAffectedActivity> MatchedActivities { get; init; } = [];
}

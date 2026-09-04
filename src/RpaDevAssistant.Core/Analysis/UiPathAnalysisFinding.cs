namespace RpaDevAssistant.Core.Analysis;

public sealed record UiPathAnalysisFinding
{
    public required string RuleId { get; init; }

    public required string RuleName { get; init; }

    public RuleSeverity Severity { get; init; }

    public RuleCategory Category { get; init; }

    public required string Message { get; init; }

    public string? Description { get; init; }

    public string? Recommendation { get; init; }

    public string? WorkflowPath { get; init; }

    public string? ActivityId { get; init; }

    public string? ActivityName { get; init; }

    public string? ActivityDisplayName { get; init; }

    public string? PropertyName { get; init; }

    public string? CurrentValue { get; init; }

    public UiPathFindingScope Scope { get; init; } = UiPathFindingScope.Activity;

    public int OccurrenceCount { get; init; } = 1;

    public int? AffectedActivityCount { get; init; }

    public int? TotalRelevantActivityCount { get; init; }

    public double? Percentage { get; init; }

    public IReadOnlyList<UiPathAffectedActivity> ExampleActivities { get; init; } = [];

    public IReadOnlyList<UiPathAffectedActivity> AffectedActivities { get; init; } = [];

    public string Source { get; init; } = "BuiltIn";
}

using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.History;

public sealed record UiPathProjectIdentity
{
    public required string ProjectId { get; init; }

    public required string ProjectPath { get; init; }

    public string? ProjectName { get; init; }

    public string? ProjectJsonHash { get; init; }
}

public sealed record UiPathFindingSnapshot
{
    public required string Id { get; init; }

    public required string ContentHash { get; init; }

    public required string RuleId { get; init; }

    public required string RuleName { get; init; }

    public required RuleSeverity Severity { get; init; }

    public required RuleCategory Category { get; init; }

    public string? WorkflowPath { get; init; }

    public string? ActivityName { get; init; }

    public string? ActivityDisplayName { get; init; }

    public string? PropertyName { get; init; }

    public string? Message { get; init; }

    public int OccurrenceCount { get; init; } = 1;
}

public sealed record UiPathWorkflowSnapshot
{
    public required string WorkflowPath { get; init; }

    public int ActivityCount { get; init; }

    public int FindingCount { get; init; }

    public int? ComplexityScore { get; init; }

    public string? ComplexityLevel { get; init; }
}

public sealed record UiPathAnalysisSnapshot
{
    public required string SnapshotId { get; init; }

    public required UiPathProjectIdentity ProjectIdentity { get; init; }

    public string? ProjectName { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public int Score { get; init; }

    public string Grade { get; init; } = "A";

    public int WorkflowCount { get; init; }

    public int TotalActivityCount { get; init; }

    public UiPathHistorySeverityCounts FindingSummary { get; init; } = new();

    public IReadOnlyList<UiPathFindingSnapshot> Findings { get; init; } = [];

    public IReadOnlyList<UiPathWorkflowSnapshot> Workflows { get; init; } = [];

    public required string ResultHash { get; init; }
}

public sealed record UiPathHistorySeverityCounts
{
    public int Total { get; init; }

    public int Critical { get; init; }

    public int Error { get; init; }

    public int Warning { get; init; }

    public int Suggestion { get; init; }

    public int Info { get; init; }
}

public sealed record UiPathAnalysisSnapshotSaveResult
{
    public required UiPathAnalysisSnapshot Snapshot { get; init; }

    public bool Saved { get; init; }

    public bool DuplicateSkipped { get; init; }

    public UiPathAnalysisComparison? ComparisonWithPrevious { get; init; }
}

public sealed record UiPathAnalysisSnapshotSummary
{
    public required string SnapshotId { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public string? ProjectName { get; init; }

    public string? ProjectPath { get; init; }

    public int Score { get; init; }

    public string Grade { get; init; } = "A";

    public int WorkflowCount { get; init; }

    public int TotalActivityCount { get; init; }

    public int TotalFindings { get; init; }

    public string? PreviousSnapshotId { get; init; }

    public int? ScoreDelta { get; init; }

    public int? TotalFindingDelta { get; init; }

    public int? NewFindingCount { get; init; }

    public int? ResolvedFindingCount { get; init; }
}

public sealed record UiPathAnalysisHistoryList
{
    public IReadOnlyList<UiPathAnalysisSnapshotSummary> Snapshots { get; init; } = [];
}

public enum UiPathFindingComparisonState
{
    New,
    Resolved,
    Unchanged,
    Changed
}

public sealed record UiPathComparedFinding
{
    public required UiPathFindingComparisonState State { get; init; }

    public required UiPathFindingSnapshot Finding { get; init; }
}

public sealed record UiPathWorkflowComparison
{
    public required string WorkflowPath { get; init; }

    public int ActivityCountDelta { get; init; }

    public int FindingCountDelta { get; init; }

    public int? ComplexityScoreDelta { get; init; }

    public int NewFindingCount { get; init; }

    public int ResolvedFindingCount { get; init; }
}

public sealed record UiPathAnalysisComparison
{
    public required string BaselineSnapshotId { get; init; }

    public required string TargetSnapshotId { get; init; }

    public int ScoreDelta { get; init; }

    public string? GradeBefore { get; init; }

    public string? GradeAfter { get; init; }

    public int TotalFindingDelta { get; init; }

    public UiPathHistorySeverityCounts SeverityBefore { get; init; } = new();

    public UiPathHistorySeverityCounts SeverityAfter { get; init; } = new();

    public UiPathHistorySeverityCounts SeverityDelta { get; init; } = new();

    public int WorkflowCountDelta { get; init; }

    public int ActivityCountDelta { get; init; }

    public IReadOnlyList<UiPathComparedFinding> NewFindings { get; init; } = [];

    public IReadOnlyList<UiPathComparedFinding> ResolvedFindings { get; init; } = [];

    public IReadOnlyList<UiPathComparedFinding> UnchangedFindings { get; init; } = [];

    public IReadOnlyList<UiPathComparedFinding> ChangedFindings { get; init; } = [];

    public IReadOnlyList<UiPathWorkflowComparison> WorkflowChanges { get; init; } = [];
}

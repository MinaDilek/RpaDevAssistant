namespace RpaDevAssistant.Core.Analysis.Scoring;

public sealed record UiPathQualityScore
{
    public int Score { get; init; }

    public required string Grade { get; init; }

    public double RawPenalty { get; init; }

    public double NormalizedPenalty { get; init; }

    public double ProjectSizeFactor { get; init; }

    public int TotalFindings { get; init; }

    public required string ProfileId { get; init; }

    public required string ProfileName { get; init; }

    public IReadOnlyList<UiPathRuleScoreBreakdown> ScoreBreakdown { get; init; } = [];

    public IReadOnlyList<UiPathSeverityScoreBreakdown> SeverityBreakdown { get; init; } = [];
}

public sealed record UiPathSeverityScoreBreakdown
{
    public RuleSeverity Severity { get; init; }

    public int FindingCount { get; init; }

    public int OccurrenceCount { get; init; }

    public double RawPenalty { get; init; }

    public double AppliedPenalty { get; init; }
}

using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.Reporting;

public sealed record UiPathReportScoreBreakdown
{
    public required string RuleId { get; init; }

    public required string RuleName { get; init; }

    public int FindingCount { get; init; }

    public int OccurrenceCount { get; init; }

    public RuleSeverity Severity { get; init; }

    public double Weight { get; init; }

    public double RawPenalty { get; init; }

    public double AppliedPenalty { get; init; }

    public double MaxPenalty { get; init; }
}

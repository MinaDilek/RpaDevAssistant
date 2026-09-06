namespace RpaDevAssistant.Core.Reporting;

using RpaDevAssistant.Core.Dependencies;
using RpaDevAssistant.Core.Flowcharts;

public sealed record UiPathAnalysisReport
{
    public required string SchemaVersion { get; init; }

    public required string ReportId { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public required string ProductName { get; init; }

    public required string ProductVersion { get; init; }

    public string? ProjectName { get; init; }

    public required string ProjectPath { get; init; }

    public string? Compatibility { get; init; }

    public bool IsReFramework { get; init; }

    public int WorkflowCount { get; init; }

    public int TotalActivityCount { get; init; }

    public required string ProfileId { get; init; }

    public required string ProfileName { get; init; }

    public int QualityScore { get; init; }

    public required string Grade { get; init; }

    public required UiPathReportSummary Summary { get; init; }

    public IReadOnlyList<UiPathReportFinding> Findings { get; init; } = [];

    public IReadOnlyList<UiPathReportWorkflow> WorkflowSummaries { get; init; } = [];

    public IReadOnlyList<UiPathReportScoreBreakdown> ScoreBreakdown { get; init; } = [];

    public IReadOnlyList<UiPathReportCount> ComplexityDistribution { get; init; } = [];

    public IReadOnlyList<UiPathReportWorkflowComplexity> TopComplexWorkflows { get; init; } = [];

    public UiPathDependencySummary? DependencyAnalysis { get; init; }

    public UiPathFlowchartAnalysisSummary? FlowchartAnalysis { get; init; }
}

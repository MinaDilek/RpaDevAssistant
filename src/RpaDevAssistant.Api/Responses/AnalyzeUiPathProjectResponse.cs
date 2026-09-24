using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Dependencies;
using RpaDevAssistant.Core.Flowcharts;
using RpaDevAssistant.Core.History;
using RpaDevAssistant.Core.Localization;
using RpaDevAssistant.Core.Compatibility;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Api.Responses;

public sealed record AnalyzeUiPathProjectResponse
{
    public bool IsValid { get; init; }

    public string? ProjectName { get; init; }

    public string ProjectPath { get; init; } = string.Empty;

    public string? Compatibility { get; init; }

    public UiPathCompatibilityBehavior? CompatibilityBehavior { get; init; }

    public int WorkflowCount { get; init; }

    public int TotalActivityCount { get; init; }

    public bool IsReFramework { get; init; }

    public UiPathReFrameworkAssessment? ReFrameworkAssessment { get; init; }

    public UiPathStaticAnalysisResult Analysis { get; init; } = new();

    public UiPathQualityScore? QualityScore { get; init; }

    public UiPathDependencySummary? DependencyAnalysis { get; init; }

    public UiPathFlowchartAnalysisSummary? FlowchartAnalysis { get; init; }

    public UiPathWorkflowComplexitySummary ComplexitySummary { get; init; } = new();

    public UiPathAnalysisSnapshotSummary? AnalysisSnapshot { get; init; }

    public UiPathAnalysisComparison? ComparisonWithPrevious { get; init; }

    public IReadOnlyList<AnalyzeWorkflowSummaryResponse> Workflows { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public string Locale { get; init; } = "en";

    public UiPathAnalysisPerformanceMetrics Performance { get; init; } = new();

    public static AnalyzeUiPathProjectResponse From(
        UiPathProjectAnalysisResult result,
        UiPathAnalysisFindingLocalizer? findingLocalizer = null,
        string? locale = null,
        UiPathAnalysisSnapshotSaveResult? snapshotSaveResult = null)
    {
        var responseLocale = SupportedLocale.Normalize(locale);
        var analysis = findingLocalizer is null ? result.Analysis : findingLocalizer.Localize(result.Analysis, responseLocale);

        return new AnalyzeUiPathProjectResponse
        {
            IsValid = result.ProjectScan.IsValid,
            ProjectName = result.ProjectName,
            ProjectPath = result.ProjectPath,
            Compatibility = result.ProjectScan.Compatibility,
            CompatibilityBehavior = result.ProjectScan.CompatibilityBehavior,
            WorkflowCount = result.WorkflowCount,
            TotalActivityCount = result.TotalActivityCount,
            IsReFramework = result.ProjectScan.IsReFramework,
            ReFrameworkAssessment = result.ProjectScan.ReFrameworkAssessment,
            Analysis = analysis,
            QualityScore = result.QualityScore,
            DependencyAnalysis = result.ProjectScan.DependencyAnalysis,
            FlowchartAnalysis = result.ProjectScan.FlowchartAnalysis,
            ComplexitySummary = result.ProjectScan.ComplexitySummary,
            AnalysisSnapshot = snapshotSaveResult is null ? null : new UiPathAnalysisSnapshotSummary
            {
                SnapshotId = snapshotSaveResult.Snapshot.SnapshotId,
                GeneratedAtUtc = snapshotSaveResult.Snapshot.GeneratedAtUtc,
                ProjectName = snapshotSaveResult.Snapshot.ProjectName,
                Score = snapshotSaveResult.Snapshot.Score,
                Grade = snapshotSaveResult.Snapshot.Grade,
                WorkflowCount = snapshotSaveResult.Snapshot.WorkflowCount,
                TotalActivityCount = snapshotSaveResult.Snapshot.TotalActivityCount,
                TotalFindings = snapshotSaveResult.Snapshot.FindingSummary.Total,
                PreviousSnapshotId = snapshotSaveResult.ComparisonWithPrevious?.BaselineSnapshotId,
                ScoreDelta = snapshotSaveResult.ComparisonWithPrevious?.ScoreDelta,
                TotalFindingDelta = snapshotSaveResult.ComparisonWithPrevious?.TotalFindingDelta,
                NewFindingCount = snapshotSaveResult.ComparisonWithPrevious?.NewFindings.Count,
                ResolvedFindingCount = snapshotSaveResult.ComparisonWithPrevious?.ResolvedFindings.Count
            },
            ComparisonWithPrevious = snapshotSaveResult?.ComparisonWithPrevious,
            Workflows = result.ProjectScan.Workflows.Select(AnalyzeWorkflowSummaryResponse.From).ToArray(),
            Errors = result.ProjectScan.Errors,
            Warnings = result.ProjectScan.Warnings,
            Locale = responseLocale,
            Performance = result.Performance
        };
    }
}

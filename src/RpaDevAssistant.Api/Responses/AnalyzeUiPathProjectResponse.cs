using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Dependencies;
using RpaDevAssistant.Core.Localization;

namespace RpaDevAssistant.Api.Responses;

public sealed record AnalyzeUiPathProjectResponse
{
    public bool IsValid { get; init; }

    public string? ProjectName { get; init; }

    public string ProjectPath { get; init; } = string.Empty;

    public int WorkflowCount { get; init; }

    public int TotalActivityCount { get; init; }

    public UiPathStaticAnalysisResult Analysis { get; init; } = new();

    public UiPathQualityScore? QualityScore { get; init; }

    public UiPathDependencySummary? DependencyAnalysis { get; init; }

    public IReadOnlyList<AnalyzeWorkflowSummaryResponse> Workflows { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public string Locale { get; init; } = "en";

    public static AnalyzeUiPathProjectResponse From(UiPathProjectAnalysisResult result, UiPathAnalysisFindingLocalizer? findingLocalizer = null, string? locale = null)
    {
        var responseLocale = SupportedLocale.Normalize(locale);
        var analysis = findingLocalizer is null ? result.Analysis : findingLocalizer.Localize(result.Analysis, responseLocale);

        return new AnalyzeUiPathProjectResponse
        {
            IsValid = result.ProjectScan.IsValid,
            ProjectName = result.ProjectName,
            ProjectPath = result.ProjectPath,
            WorkflowCount = result.WorkflowCount,
            TotalActivityCount = result.TotalActivityCount,
            Analysis = analysis,
            QualityScore = result.QualityScore,
            DependencyAnalysis = result.ProjectScan.DependencyAnalysis,
            Workflows = result.ProjectScan.Workflows.Select(AnalyzeWorkflowSummaryResponse.From).ToArray(),
            Errors = result.ProjectScan.Errors,
            Warnings = result.ProjectScan.Warnings,
            Locale = responseLocale
        };
    }
}

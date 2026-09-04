using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis;

public sealed record UiPathProjectAnalysisResult
{
    public required ProjectScanResult ProjectScan { get; init; }

    public required UiPathStaticAnalysisResult Analysis { get; init; }

    public required UiPathQualityScore QualityScore { get; init; }

    public required UiPathRuleProfile Profile { get; init; }

    public string? ProjectName => ProjectScan.ProjectName;

    public string ProjectPath => ProjectScan.ProjectPath;

    public int WorkflowCount => ProjectScan.WorkflowCount;

    public int TotalActivityCount => ProjectScan.TotalActivityCount;
}

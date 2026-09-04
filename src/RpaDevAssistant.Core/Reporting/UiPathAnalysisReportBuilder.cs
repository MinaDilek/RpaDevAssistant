using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Reporting;

public sealed class UiPathAnalysisReportBuilder : IUiPathAnalysisReportBuilder
{
    public UiPathAnalysisReport Build(
        ProjectScanResult projectScan,
        UiPathStaticAnalysisResult analysis,
        UiPathQualityScore qualityScore,
        UiPathRuleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(projectScan);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(qualityScore);
        ArgumentNullException.ThrowIfNull(profile);

        var orderedFindings = analysis.Findings
            .OrderBy(finding => GetSeveritySortOrder(finding.Severity))
            .ThenBy(finding => finding.WorkflowPath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.RuleId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.ActivityDisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(MapFinding)
            .ToArray();

        return new UiPathAnalysisReport
        {
            SchemaVersion = ProductInfo.ReportSchemaVersion,
            ReportId = Guid.NewGuid().ToString("N"),
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            ProductName = ProductInfo.ProductName,
            ProductVersion = ProductInfo.ProductVersion,
            ProjectName = projectScan.ProjectName,
            ProjectPath = projectScan.ProjectPath,
            Compatibility = projectScan.Compatibility,
            IsReFramework = projectScan.IsReFramework,
            WorkflowCount = projectScan.WorkflowCount,
            TotalActivityCount = projectScan.TotalActivityCount,
            ProfileId = profile.Id,
            ProfileName = profile.Name,
            QualityScore = qualityScore.Score,
            Grade = qualityScore.Grade,
            Summary = BuildSummary(projectScan, analysis),
            Findings = orderedFindings,
            WorkflowSummaries = BuildWorkflowSummaries(projectScan, analysis),
            ScoreBreakdown = qualityScore.ScoreBreakdown.Select(MapScoreBreakdown).ToArray(),
            ComplexityDistribution = BuildComplexityDistribution(projectScan),
            TopComplexWorkflows = projectScan.TopComplexWorkflows.Select(MapWorkflowComplexity).ToArray(),
            DependencyAnalysis = projectScan.DependencyAnalysis
        };
    }

    public static int GetSeveritySortOrder(RuleSeverity severity)
    {
        return severity switch
        {
            RuleSeverity.Critical => 0,
            RuleSeverity.Error => 1,
            RuleSeverity.Warning => 2,
            RuleSeverity.Suggestion => 3,
            RuleSeverity.Info => 4,
            _ => 5
        };
    }

    private static UiPathReportSummary BuildSummary(ProjectScanResult projectScan, UiPathStaticAnalysisResult analysis)
    {
        var workflowsWithFindings = analysis.Findings
            .Where(finding => !string.IsNullOrWhiteSpace(finding.WorkflowPath))
            .Select(finding => finding.WorkflowPath!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new UiPathReportSummary
        {
            TotalFindings = analysis.TotalFindings,
            CriticalCount = analysis.CriticalCount,
            ErrorCount = analysis.ErrorCount,
            WarningCount = analysis.WarningCount,
            SuggestionCount = analysis.SuggestionCount,
            InfoCount = analysis.InfoCount,
            WorkflowsWithFindings = workflowsWithFindings,
            CleanWorkflows = Math.Max(0, projectScan.WorkflowCount - workflowsWithFindings),
            TopCategories = BuildTopCounts(analysis.Findings.Select(finding => finding.Category.ToString())),
            TopRules = BuildTopCounts(analysis.Findings.Select(finding => $"{finding.RuleId} {finding.RuleName}"))
        };
    }

    private static IReadOnlyList<UiPathReportWorkflow> BuildWorkflowSummaries(ProjectScanResult projectScan, UiPathStaticAnalysisResult analysis)
    {
        var findingsByWorkflow = analysis.Findings
            .Where(finding => !string.IsNullOrWhiteSpace(finding.WorkflowPath))
            .GroupBy(finding => finding.WorkflowPath!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        return projectScan.Workflows
            .OrderBy(workflow => workflow.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(workflow =>
            {
                findingsByWorkflow.TryGetValue(workflow.RelativePath, out var findings);
                findings ??= [];
                return new UiPathReportWorkflow
                {
                    RelativePath = workflow.RelativePath,
                    ActivityCount = workflow.ActivityCount,
                    FindingCount = findings.Length,
                    CriticalCount = CountSeverity(findings, RuleSeverity.Critical),
                    ErrorCount = CountSeverity(findings, RuleSeverity.Error),
                    WarningCount = CountSeverity(findings, RuleSeverity.Warning),
                    SuggestionCount = CountSeverity(findings, RuleSeverity.Suggestion),
                    InfoCount = CountSeverity(findings, RuleSeverity.Info),
                    Complexity = workflow.Analysis?.Complexity is null ? null : MapWorkflowComplexity(workflow.Analysis.Complexity),
                    Status = CalculateWorkflowStatus(findings)
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<UiPathReportCount> BuildComplexityDistribution(ProjectScanResult projectScan)
    {
        return projectScan.Workflows
            .Select(workflow => workflow.Analysis?.Complexity)
            .Where(complexity => complexity is not null)
            .GroupBy(complexity => complexity!.ComplexityLevel.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new UiPathReportCount
            {
                Name = group.Key,
                Count = group.Count()
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string CalculateWorkflowStatus(IReadOnlyCollection<UiPathAnalysisFinding> findings)
    {
        if (findings.Count == 0)
        {
            return "Clean";
        }

        return findings.Any(finding => finding.Severity is RuleSeverity.Critical or RuleSeverity.Error)
            ? "High Risk"
            : "Attention";
    }

    private static int CountSeverity(IEnumerable<UiPathAnalysisFinding> findings, RuleSeverity severity)
    {
        return findings.Count(finding => finding.Severity == severity);
    }

    private static IReadOnlyList<UiPathReportCount> BuildTopCounts(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new UiPathReportCount
            {
                Name = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
    }

    private static UiPathReportFinding MapFinding(UiPathAnalysisFinding finding)
    {
        return new UiPathReportFinding
        {
            RuleId = finding.RuleId,
            RuleName = finding.RuleName,
            Severity = finding.Severity,
            Category = finding.Category,
            Message = finding.Message,
            Description = finding.Description,
            Recommendation = finding.Recommendation,
            WorkflowPath = finding.WorkflowPath,
            ActivityId = finding.ActivityId,
            ActivityName = finding.ActivityName,
            ActivityDisplayName = finding.ActivityDisplayName,
            PropertyName = finding.PropertyName,
            CurrentValue = finding.CurrentValue,
            Scope = finding.Scope,
            OccurrenceCount = finding.OccurrenceCount,
            AffectedActivityCount = finding.AffectedActivityCount,
            TotalRelevantActivityCount = finding.TotalRelevantActivityCount,
            Percentage = finding.Percentage,
            ExampleActivities = finding.ExampleActivities,
            AffectedActivities = finding.AffectedActivities,
            Source = finding.Source
        };
    }

    private static UiPathReportScoreBreakdown MapScoreBreakdown(UiPathRuleScoreBreakdown breakdown)
    {
        return new UiPathReportScoreBreakdown
        {
            RuleId = breakdown.RuleId,
            RuleName = breakdown.RuleName,
            FindingCount = breakdown.FindingCount,
            OccurrenceCount = breakdown.OccurrenceCount,
            Severity = breakdown.Severity,
            Weight = breakdown.Weight,
            RawPenalty = breakdown.RawPenalty,
            AppliedPenalty = breakdown.AppliedPenalty,
            MaxPenalty = breakdown.MaxPenalty
        };
    }

    private static UiPathReportWorkflowComplexity MapWorkflowComplexity(UiPathWorkflowComplexity complexity)
    {
        return new UiPathReportWorkflowComplexity
        {
            WorkflowPath = complexity.WorkflowPath,
            TotalActivities = complexity.TotalActivities,
            ExecutableActivities = complexity.ExecutableActivities,
            ContainerActivities = complexity.ContainerActivities,
            MaxNestingDepth = complexity.MaxNestingDepth,
            DecisionCount = complexity.DecisionCount,
            IfCount = complexity.IfCount,
            SwitchCount = complexity.SwitchCount,
            LoopCount = complexity.LoopCount,
            TryCatchCount = complexity.TryCatchCount,
            InvokeWorkflowCount = complexity.InvokeWorkflowCount,
            ArgumentCount = complexity.ArgumentCount,
            FindingCount = complexity.FindingCount,
            ComplexityScore = complexity.ComplexityScore,
            ComplexityLevel = complexity.ComplexityLevel
        };
    }
}

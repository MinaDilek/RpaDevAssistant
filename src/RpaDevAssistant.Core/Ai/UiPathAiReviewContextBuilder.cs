using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Ai;

public sealed class UiPathAiReviewContextBuilder : IUiPathAiReviewContextBuilder
{
    private readonly ISecretRedactor redactor;
    private readonly UiPathAiReviewOptions options;

    public UiPathAiReviewContextBuilder(ISecretRedactor redactor, UiPathAiReviewOptions? options = null)
    {
        this.redactor = redactor;
        this.options = options ?? new UiPathAiReviewOptions();
    }

    public UiPathAiReviewRequest Build(
        ProjectScanResult projectScan,
        UiPathStaticAnalysisResult staticAnalysis,
        UiPathQualityScore qualityScore,
        UiPathAiReviewScope scope,
        string? workflowPath,
        string? additionalInstructions = null,
        string? locale = null)
    {
        ArgumentNullException.ThrowIfNull(projectScan);
        ArgumentNullException.ThrowIfNull(staticAnalysis);
        ArgumentNullException.ThrowIfNull(qualityScore);

        var selectedWorkflow = SelectWorkflows(projectScan, scope, workflowPath).ToArray();
        var selectedWorkflowPaths = selectedWorkflow.Select(workflow => workflow.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new UiPathAiReviewRequest
        {
            ProjectName = projectScan.ProjectName,
            Compatibility = projectScan.Compatibility,
            IsReFramework = projectScan.IsReFramework,
            WorkflowCount = projectScan.WorkflowCount,
            TotalActivityCount = projectScan.TotalActivityCount,
            SelectedWorkflow = workflowPath,
            Workflows = selectedWorkflow.Select(BuildWorkflowContext).ToArray(),
            DeterministicFindings = SelectFindings(staticAnalysis, scope, selectedWorkflowPaths).ToArray(),
            Dependencies = projectScan.Dependencies,
            ReviewScope = scope,
            AdditionalInstructions = additionalInstructions,
            Locale = locale
        };
    }

    private IEnumerable<UiPathWorkflowInfo> SelectWorkflows(ProjectScanResult projectScan, UiPathAiReviewScope scope, string? workflowPath)
    {
        if (scope == UiPathAiReviewScope.Workflow)
        {
            return projectScan.Workflows.Where(workflow => workflow.RelativePath.Equals(workflowPath, StringComparison.OrdinalIgnoreCase));
        }

        return projectScan.Workflows
            .OrderByDescending(workflow => workflow.ActivityCount)
            .ThenBy(workflow => workflow.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaxWorkflows);
    }

    private IEnumerable<UiPathAnalysisFinding> SelectFindings(UiPathStaticAnalysisResult staticAnalysis, UiPathAiReviewScope scope, HashSet<string> selectedWorkflowPaths)
    {
        var findings = staticAnalysis.Findings.AsEnumerable();
        if (scope == UiPathAiReviewScope.Workflow)
        {
            findings = findings.Where(finding => finding.WorkflowPath is not null && selectedWorkflowPaths.Contains(finding.WorkflowPath));
        }

        return findings
            .OrderBy(finding => GetSeveritySortOrder(finding.Severity))
            .ThenBy(finding => finding.WorkflowPath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.RuleId, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaxFindings);
    }

    private UiPathAiWorkflowContext BuildWorkflowContext(UiPathWorkflowInfo workflow)
    {
        var analysis = workflow.Analysis;
        if (analysis is null)
        {
            return new UiPathAiWorkflowContext
            {
                RelativePath = workflow.RelativePath,
                ActivityCount = workflow.ActivityCount
            };
        }

        var selectedActivities = analysis.Activities.Take(options.MaxActivitiesPerWorkflow).ToArray();
        return new UiPathAiWorkflowContext
        {
            RelativePath = workflow.RelativePath,
            ActivityCount = analysis.ActivityCount,
            Arguments = analysis.Arguments.Select(argument => $"{argument.Name} : {argument.Direction} {argument.Type}".Trim()).ToArray(),
            Activities = selectedActivities.Select(FormatActivity).ToArray(),
            InvokeReferences = analysis.Activities
                .Where(activity => activity.Properties.ContainsKey("WorkflowFileName"))
                .Select(activity => Truncate(activity.Properties["WorkflowFileName"]))
                .ToArray(),
            IsTruncated = analysis.Activities.Count > selectedActivities.Length
        };
    }

    private string FormatActivity(UiPathActivityInfo activity)
    {
        var display = activity.DisplayName.Equals(activity.Name, StringComparison.OrdinalIgnoreCase)
            ? activity.Name
            : $"{activity.Name} - {activity.DisplayName}";
        var properties = activity.Properties.Count == 0
            ? string.Empty
            : " | " + string.Join(", ", activity.Properties.Select(property => $"{property.Key}={Truncate(redactor.Redact(property.Key, property.Value))}"));

        return $"{new string(' ', activity.Depth * 2)}{display}{properties}";
    }

    private string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= options.MaxPropertyLength ? value : $"{value[..options.MaxPropertyLength]} [TRUNCATED]";
    }

    private static int GetSeveritySortOrder(RuleSeverity severity)
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
}

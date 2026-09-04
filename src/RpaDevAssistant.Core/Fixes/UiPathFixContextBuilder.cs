using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.ProjectAssistant;

namespace RpaDevAssistant.Core.Fixes;

public sealed class UiPathFixContextBuilder : IUiPathFixContextBuilder
{
    private readonly IUiPathWorkflowGraphBuilder graphBuilder;

    public UiPathFixContextBuilder(IUiPathWorkflowGraphBuilder graphBuilder)
    {
        this.graphBuilder = graphBuilder;
    }

    public UiPathFixContext Build(UiPathProjectAnalysisResult analysis, UiPathAnalysisFinding finding, string? locale = null)
    {
        var workflow = analysis.ProjectScan.Workflows.FirstOrDefault(item =>
            finding.WorkflowPath is not null && item.RelativePath.Equals(finding.WorkflowPath, StringComparison.OrdinalIgnoreCase));
        var activity = workflow?.Analysis?.Activities.FirstOrDefault(item =>
            (finding.ActivityId is not null && item.ActivityId.Equals(finding.ActivityId, StringComparison.OrdinalIgnoreCase))
            || (finding.ActivityName is not null && item.Name.Equals(finding.ActivityName, StringComparison.OrdinalIgnoreCase)
                && (finding.ActivityDisplayName is null || item.DisplayName.Equals(finding.ActivityDisplayName, StringComparison.OrdinalIgnoreCase))));
        var parent = activity?.ParentActivityId is null
            ? null
            : workflow?.Analysis?.Activities.FirstOrDefault(item => item.ActivityId.Equals(activity.ParentActivityId, StringComparison.OrdinalIgnoreCase));

        return new UiPathFixContext
        {
            Project = analysis.ProjectScan,
            Workflow = workflow,
            Activity = activity,
            ParentActivity = parent,
            NearbyActivities = GetNearbyActivities(workflow?.Analysis?.Activities ?? [], activity),
            Finding = finding,
            RelatedFindings = analysis.Analysis.Findings
                .Where(item => item.WorkflowPath?.Equals(finding.WorkflowPath, StringComparison.OrdinalIgnoreCase) == true)
                .ToArray(),
            RuleProfile = analysis.Profile,
            InvocationGraph = graphBuilder.Build(analysis.ProjectScan),
            Locale = locale
        };
    }

    private static IReadOnlyList<UiPathActivityInfo> GetNearbyActivities(IReadOnlyList<UiPathActivityInfo> activities, UiPathActivityInfo? activity)
    {
        if (activity is null)
        {
            return [];
        }

        var index = activities.ToList().FindIndex(item => item.ActivityId.Equals(activity.ActivityId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return [];
        }

        var start = Math.Max(0, index - 2);
        var count = Math.Min(5, activities.Count - start);
        return activities.Skip(start).Take(count).ToArray();
    }
}

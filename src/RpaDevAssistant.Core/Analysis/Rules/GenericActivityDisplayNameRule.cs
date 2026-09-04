namespace RpaDevAssistant.Core.Analysis.Rules;

using RpaDevAssistant.Core.Models;

public sealed class GenericActivityDisplayNameRule : UiPathAnalysisRuleBase
{
    private static readonly HashSet<string> CheckedActivityNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Click",
        "TypeInto",
        "GetText",
        "InvokeWorkflowFile",
        "LogMessage",
        "HTTPRequest",
        "Assign"
    };

    public override string Id => "RPA007";

    public override string Name => "Generic Activity Display Name";

    public override string Description => "Important activities should have display names that explain intent.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Maintainability;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            var relevantActivities = workflow.Activities
                .Where(IsCheckedActivity)
                .ToArray();
            var affectedActivities = relevantActivities
                .Where(activity => IsGenericDisplayName(activity.Name, activity.DisplayName))
                .ToArray();
            if (affectedActivities.Length == 0)
            {
                continue;
            }

            var affectedActivityDetails = affectedActivities
                .Select(ToAffectedActivity)
                .ToArray();
            var percentage = relevantActivities.Length == 0
                ? 0
                : affectedActivities.Length * 100.0 / relevantActivities.Length;

            yield return new UiPathAnalysisFinding
            {
                RuleId = Id,
                RuleName = Name,
                Severity = Severity,
                Category = Category,
                Message = $"{affectedActivities.Length} generic activity display names detected in workflow.",
                Description = Description,
                Recommendation = "Rename generic activity display names to describe the business or UI action being performed.",
                WorkflowPath = workflow.RelativePath,
                PropertyName = "DisplayName",
                CurrentValue = $"{affectedActivities.Length}/{relevantActivities.Length}",
                Scope = UiPathFindingScope.Aggregated,
                OccurrenceCount = affectedActivities.Length,
                AffectedActivityCount = affectedActivities.Length,
                TotalRelevantActivityCount = relevantActivities.Length,
                Percentage = Math.Round(percentage, 2),
                ExampleActivities = affectedActivityDetails.Take(5).ToArray(),
                AffectedActivities = affectedActivityDetails
            };
        }
    }

    private static bool IsCheckedActivity(UiPathActivityInfo activity)
    {
        return CheckedActivityNames.Contains(UiPathActivityClassifier.NormalizeActivityName(activity.Name));
    }

    private static bool IsGenericDisplayName(string activityName, string displayName)
    {
        var normalizedName = UiPathActivityClassifier.NormalizeActivityName(activityName);
        var normalizedDisplayName = UiPathActivityClassifier.NormalizeActivityName(displayName);
        return normalizedDisplayName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase);
    }

    private static UiPathAffectedActivity ToAffectedActivity(UiPathActivityInfo activity)
    {
        return new UiPathAffectedActivity
        {
            ActivityId = activity.ActivityId,
            StableId = activity.StableId,
            ActivityPath = activity.ActivityPath,
            ActivityName = activity.Name,
            ActivityDisplayName = activity.DisplayName,
            PropertyName = "DisplayName",
            CurrentValue = activity.DisplayName
        };
    }
}

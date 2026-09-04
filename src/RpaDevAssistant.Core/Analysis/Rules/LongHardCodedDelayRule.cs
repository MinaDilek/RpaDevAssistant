namespace RpaDevAssistant.Core.Analysis.Rules;

public sealed class LongHardCodedDelayRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA004";

    public override string Name => "Long Hard-Coded Delay";

    public override string Description => "Long fixed delays slow down robots and often indicate brittle synchronization.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override RuleCategory Category => RuleCategory.Performance;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities.Where(activity => UiPathActivityClassifier.IsNamed(activity, "Delay")))
            {
                if (!activity.Properties.TryGetValue("Duration", out var duration) || !TryParseDuration(duration, out var parsedDuration))
                {
                    continue;
                }

                if (parsedDuration < TimeSpan.FromSeconds(5))
                {
                    continue;
                }

                yield return CreateFinding(
                    "Delay duration is 5 seconds or longer.",
                    "Prefer state-based waiting or move timeout values into configuration.",
                    workflow,
                    activity,
                    "Duration",
                    duration);
            }
        }
    }

    private static bool TryParseDuration(string? value, out TimeSpan duration)
    {
        if (TimeSpan.TryParse(value, out duration))
        {
            return true;
        }

        duration = default;
        return false;
    }
}

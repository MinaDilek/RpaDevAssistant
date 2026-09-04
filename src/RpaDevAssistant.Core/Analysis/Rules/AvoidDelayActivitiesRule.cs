namespace RpaDevAssistant.Core.Analysis.Rules;

public sealed class AvoidDelayActivitiesRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA001";

    public override string Name => "Avoid Delay Activities";

    public override string Description => "Delay activities can make automations brittle when application response times vary.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override RuleCategory Category => RuleCategory.Reliability;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities.Where(activity => UiPathActivityClassifier.IsNamed(activity, "Delay")))
            {
                activity.Properties.TryGetValue("Duration", out var duration);
                yield return CreateFinding(
                    "Delay activity detected. Prefer state-based waiting mechanisms when possible.",
                    "Use Check App State, Retry Scope, Element Exists, or timeout-based UI activities where possible.",
                    workflow,
                    activity,
                    duration is null ? null : "Duration",
                    duration);
            }
        }
    }
}

namespace RpaDevAssistant.Core.Analysis.Rules;

public sealed class WorkflowHasNoLoggingRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA008";

    public override string Name => "Workflow Has No Logging";

    public override string Description => "Larger workflows should include logging to support operations and troubleshooting.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Logging;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            var executableCount = workflow.Activities.Count(UiPathActivityClassifier.IsExecutable);
            if (executableCount <= 5 || workflow.Activities.Any(activity => UiPathActivityClassifier.IsNamed(activity, "LogMessage")))
            {
                continue;
            }

            yield return CreateFinding(
                "Workflow has more than 5 executable activities but no Log Message activity.",
                "Add meaningful Log Message activities at key workflow steps.",
                workflow,
                propertyName: "Workflow",
                currentValue: workflow.RelativePath);
        }
    }
}

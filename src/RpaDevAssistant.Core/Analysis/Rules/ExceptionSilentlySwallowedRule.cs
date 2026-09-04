namespace RpaDevAssistant.Core.Analysis.Rules;

public sealed class ExceptionSilentlySwallowedRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA003";

    public override string Name => "Exception Silently Swallowed";

    public override string Description => "Catch blocks should log, throw, or rethrow exceptions to avoid hiding failures.";

    public override RuleSeverity Severity => RuleSeverity.Error;

    public override RuleCategory Category => RuleCategory.ExceptionHandling;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var catchActivity in workflow.Activities.Where(activity => UiPathActivityClassifier.IsNamed(activity, "Catch")))
            {
                var descendants = UiPathActivityClassifier.DescendantsOf(workflow, catchActivity);
                if (descendants.Any(activity => UiPathActivityClassifier.IsNamed(activity, "LogMessage", "Throw", "Rethrow")))
                {
                    continue;
                }

                yield return CreateFinding(
                    "Catch block does not log, throw, or rethrow the exception.",
                    "Add Log Message for observability or use Throw/Rethrow when the exception should propagate.",
                    workflow,
                    catchActivity);
            }
        }
    }
}

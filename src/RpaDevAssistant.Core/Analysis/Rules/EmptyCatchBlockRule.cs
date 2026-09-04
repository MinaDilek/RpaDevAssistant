namespace RpaDevAssistant.Core.Analysis.Rules;

public sealed class EmptyCatchBlockRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA002";

    public override string Name => "Empty Catch Block";

    public override string Description => "Catch blocks should handle exceptions explicitly instead of doing nothing.";

    public override RuleSeverity Severity => RuleSeverity.Error;

    public override RuleCategory Category => RuleCategory.ExceptionHandling;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var catchActivity in workflow.Activities.Where(activity => UiPathActivityClassifier.IsNamed(activity, "Catch")))
            {
                var descendants = UiPathActivityClassifier.DescendantsOf(workflow, catchActivity);
                if (descendants.Any(UiPathActivityClassifier.IsExecutable))
                {
                    continue;
                }

                yield return CreateFinding(
                    "Empty Catch block detected.",
                    "Add explicit exception handling such as logging, recovery action, Throw, or Rethrow.",
                    workflow,
                    catchActivity);
            }
        }
    }
}

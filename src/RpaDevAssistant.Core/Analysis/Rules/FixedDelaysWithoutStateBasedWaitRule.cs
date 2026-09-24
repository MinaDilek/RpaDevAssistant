using System.Globalization;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis.Rules;

public sealed class FixedDelaysWithoutStateBasedWaitRule : UiPathAnalysisRuleBase
{
    private readonly IUiPathExpressionClassifier expressionClassifier;

    public FixedDelaysWithoutStateBasedWaitRule(IUiPathExpressionClassifier expressionClassifier)
    {
        this.expressionClassifier = expressionClassifier;
    }

    public override string Id => "RPA046";

    public override string Name => "Fixed Delays Without State-Based Wait";

    public override string Description => "Detects workflows that repeatedly use long fixed delays without an observable state-based wait.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Reliability;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            var fixedDelayCount = workflow.Activities.Count(IsLongFixedDelay);
            if (fixedDelayCount < UiPathAnalysisThresholds.Default.FixedDelayWithoutStateWaitThreshold ||
                !workflow.Activities.Any(UiPathWorkflowMetricsCalculator.IsUiActivity))
            {
                continue;
            }

            var retryScopeCount = workflow.Activities.Count(activity =>
                UiPathActivityClassifier.IsNamed(activity, "RetryScope", "Retry Scope"));
            var checkAppStateCount = workflow.Activities.Count(activity =>
                UiPathActivityClassifier.IsNamed(activity, "CheckAppState", "Check App State"));
            if (retryScopeCount > 0 || checkAppStateCount > 0)
            {
                continue;
            }

            yield return CreateFinding(
                $"Workflow uses {fixedDelayCount} long fixed Delay activities; Retry Scope or Check App State was not detected in this workflow.",
                "Where an observable state exists, replace repeated fixed waits with Retry Scope or Check App State.",
                workflow,
                currentValue: $"fixedDelays={fixedDelayCount}; retryScopes={retryScopeCount}; checkAppStates={checkAppStateCount}");
        }
    }

    private bool IsLongFixedDelay(UiPathActivityInfo activity)
    {
        if (!UiPathActivityClassifier.IsNamed(activity, "Delay") ||
            !UiPathPropertyLookup.TryGet(activity, out var duration, "Duration"))
        {
            return false;
        }

        var classification = expressionClassifier.Classify(duration);
        return classification.IsHardCodedLiteral &&
            TimeSpan.TryParse(classification.LiteralValue, CultureInfo.InvariantCulture, out var parsedDuration) &&
            parsedDuration >= TimeSpan.FromSeconds(UiPathAnalysisThresholds.Default.LongDelaySeconds);
    }
}

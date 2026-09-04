using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis.Rules;

public abstract class UiPathAnalysisRuleBase : IUiPathAnalysisRule
{
    public abstract string Id { get; }

    public abstract string Name { get; }

    public abstract string Description { get; }

    public abstract RuleSeverity Severity { get; }

    public abstract RuleCategory Category { get; }

    public abstract IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context);

    protected UiPathAnalysisFinding CreateFinding(
        string message,
        string? recommendation = null,
        UiPathWorkflowAnalysis? workflow = null,
        UiPathActivityInfo? activity = null,
        string? propertyName = null,
        string? currentValue = null)
    {
        return new UiPathAnalysisFinding
        {
            RuleId = Id,
            RuleName = Name,
            Severity = Severity,
            Category = Category,
            Message = message,
            Description = Description,
            Recommendation = recommendation,
            WorkflowPath = workflow?.RelativePath,
            ActivityId = activity?.ActivityId,
            ActivityName = activity?.Name,
            ActivityDisplayName = activity?.DisplayName,
            PropertyName = propertyName,
            CurrentValue = currentValue
        };
    }
}

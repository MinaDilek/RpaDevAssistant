namespace RpaDevAssistant.Core.Reporting;

public sealed record UiPathExecutiveSummary
{
    public required string RiskLevel { get; init; }

    public int CriticalAndErrorFindings { get; init; }

    public int WorkflowsRequiringAttention { get; init; }

    public string? MostAffectedWorkflow { get; init; }

    public int MostAffectedWorkflowFindingCount { get; init; }

    public IReadOnlyList<string> PriorityRuleIds { get; init; } = [];
}

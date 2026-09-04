using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.Reporting;

public sealed record UiPathReportWorkflowComplexity
{
    public required string WorkflowPath { get; init; }

    public int TotalActivities { get; init; }

    public int ExecutableActivities { get; init; }

    public int ContainerActivities { get; init; }

    public int MaxNestingDepth { get; init; }

    public int DecisionCount { get; init; }

    public int IfCount { get; init; }

    public int SwitchCount { get; init; }

    public int LoopCount { get; init; }

    public int TryCatchCount { get; init; }

    public int InvokeWorkflowCount { get; init; }

    public int ArgumentCount { get; init; }

    public int FindingCount { get; init; }

    public int ComplexityScore { get; init; }

    public UiPathWorkflowComplexityLevel ComplexityLevel { get; init; }
}

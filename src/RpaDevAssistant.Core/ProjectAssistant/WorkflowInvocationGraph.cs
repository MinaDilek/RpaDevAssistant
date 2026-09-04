namespace RpaDevAssistant.Core.ProjectAssistant;

public sealed record WorkflowInvocationGraph
{
    public IReadOnlyList<string> Workflows { get; init; } = [];

    public IReadOnlyList<WorkflowInvocationEdge> Edges { get; init; } = [];
}

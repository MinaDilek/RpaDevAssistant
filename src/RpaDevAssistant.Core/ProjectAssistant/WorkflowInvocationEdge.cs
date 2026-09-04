namespace RpaDevAssistant.Core.ProjectAssistant;

public sealed record WorkflowInvocationEdge
{
    public required string CallerWorkflowPath { get; init; }

    public string? CalleeWorkflowPath { get; init; }

    public required string RawReference { get; init; }

    public bool IsDynamicReference { get; init; }
}

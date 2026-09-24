namespace RpaDevAssistant.Api.Requests;

public sealed record RenameUiPathWorkflowRequest
{
    public string? ProjectPath { get; init; }

    public string? WorkflowPath { get; init; }

    public string? NewWorkflowPath { get; init; }

    public string? ExpectedFileHash { get; init; }
}

namespace RpaDevAssistant.Core.Fixes.Rename;

public sealed record UiPathWorkflowRenameRequest
{
    public required string ProjectPath { get; init; }

    public required string WorkflowPath { get; init; }

    public required string NewWorkflowPath { get; init; }

    public string? ExpectedFileHash { get; init; }

    public bool CreateBackup { get; init; } = true;
}

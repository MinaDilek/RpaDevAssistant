namespace RpaDevAssistant.Api.Requests;

public sealed record UndoFixUiPathProjectRequest
{
    public string? ProjectPath { get; init; }

    public string? BackupId { get; init; }

    public string? WorkflowPath { get; init; }

    public string? ExpectedCurrentHash { get; init; }

    public bool CreateSafetyBackup { get; init; } = true;
}

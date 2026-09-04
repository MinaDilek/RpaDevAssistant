namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathUndoRequest
{
    public required string ProjectPath { get; init; }

    public required string BackupId { get; init; }

    public required string WorkflowPath { get; init; }

    public string? ExpectedCurrentHash { get; init; }

    public bool CreateSafetyBackup { get; init; } = true;
}

namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathSafetyBackupMetadata
{
    public required string SafetyBackupId { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public required string SourceBackupId { get; init; }

    public required string WorkflowPath { get; init; }

    public required string CurrentHash { get; init; }

    public string Reason { get; init; } = "pre-undo";
}

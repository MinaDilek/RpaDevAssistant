namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathSafetyBackupResult
{
    public required string SafetyBackupId { get; init; }

    public required string BackupFilePath { get; init; }

    public required string BackupRootPath { get; init; }
}

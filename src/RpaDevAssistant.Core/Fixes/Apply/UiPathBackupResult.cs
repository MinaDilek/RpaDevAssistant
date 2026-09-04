namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathBackupResult
{
    public required string BackupId { get; init; }

    public required string BackupRootPath { get; init; }

    public required string BackupFilePath { get; init; }
}

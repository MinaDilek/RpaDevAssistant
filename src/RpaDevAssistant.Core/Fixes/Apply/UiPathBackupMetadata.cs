namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathBackupMetadata
{
    public required string BackupId { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public string ProductVersion { get; init; } = "0.1.0";

    public required string OriginalProjectPath { get; init; }

    public IReadOnlyList<UiPathBackupMetadataFile> Files { get; init; } = [];
}

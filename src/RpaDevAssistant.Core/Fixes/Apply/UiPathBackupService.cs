using System.Text.Json;

namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed class UiPathBackupService : IUiPathBackupService
{
    public UiPathBackupResult CreateBackup(
        string projectPath,
        string workflowPath,
        string workflowFullPath,
        string originalHash,
        string modifiedHash,
        string ruleId,
        string propertyName,
        string? previousValue,
        string? newValue)
    {
        var backupId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var backupRoot = Path.Combine(projectPath, ".rpadevassistant", "backups", backupId);
        var backupFilePath = Path.Combine(backupRoot, workflowPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath)!);
        File.Copy(workflowFullPath, backupFilePath, overwrite: false);

        var metadata = new UiPathBackupMetadata
        {
            BackupId = backupId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            OriginalProjectPath = projectPath,
            Files =
            [
                new UiPathBackupMetadataFile
                {
                    WorkflowPath = workflowPath,
                    OriginalHash = originalHash,
                    ModifiedHash = modifiedHash,
                    AppliedRuleId = ruleId,
                    PropertyName = propertyName,
                    PreviousValue = previousValue,
                    NewValue = newValue
                }
            ]
        };

        File.WriteAllText(
            Path.Combine(backupRoot, "backup.json"),
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));

        return new UiPathBackupResult
        {
            BackupId = backupId,
            BackupRootPath = backupRoot,
            BackupFilePath = backupFilePath
        };
    }
}

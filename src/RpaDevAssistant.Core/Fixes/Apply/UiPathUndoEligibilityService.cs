namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed class UiPathUndoEligibilityService : IUiPathUndoEligibilityService
{
    public UiPathBackupSummary Evaluate(string projectPath, string backupId, UiPathBackupMetadata? metadata)
    {
        var invalid = ValidateMetadata(backupId, metadata);
        if (invalid is not null)
        {
            return new UiPathBackupSummary
            {
                BackupId = backupId,
                CreatedAtUtc = metadata?.CreatedAtUtc,
                Status = UiPathBackupStatus.InvalidBackup,
                CanUndo = false,
                Reason = invalid
            };
        }

        var file = metadata!.Files[0];
        var currentPath = UiPathPathSafety.ResolveProjectFile(projectPath, file.WorkflowPath);
        if (currentPath is null || !File.Exists(currentPath))
        {
            return Summary(backupId, metadata, file, UiPathBackupStatus.MissingFile, false, "Current workflow file is missing.");
        }

        var backupPath = Path.Combine(projectPath, ".rpadevassistant", "backups", backupId, file.WorkflowPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(backupPath))
        {
            return Summary(backupId, metadata, file, UiPathBackupStatus.InvalidBackup, false, "Backed-up workflow file is missing.");
        }

        try
        {
            if (!string.Equals(UiPathFileHash.Sha256(backupPath), file.OriginalHash, StringComparison.OrdinalIgnoreCase))
            {
                return Summary(backupId, metadata, file, UiPathBackupStatus.InvalidBackup, false, "Backup integrity validation failed.");
            }

            var currentHash = UiPathFileHash.Sha256(currentPath);
            if (string.Equals(currentHash, file.ModifiedHash, StringComparison.OrdinalIgnoreCase))
            {
                return Summary(backupId, metadata, file, UiPathBackupStatus.Available, true, null);
            }

            if (string.Equals(currentHash, file.OriginalHash, StringComparison.OrdinalIgnoreCase))
            {
                return Summary(backupId, metadata, file, UiPathBackupStatus.AlreadyRestored, false, "Workflow is already in the state represented by this backup.");
            }

            return Summary(backupId, metadata, file, UiPathBackupStatus.CurrentFileChanged, false, "A newer modification or external change exists for this workflow.");
        }
        catch (IOException)
        {
            return Summary(backupId, metadata, file, UiPathBackupStatus.MissingFile, false, "Workflow file could not be read.");
        }
    }

    private static string? ValidateMetadata(string backupId, UiPathBackupMetadata? metadata)
    {
        if (metadata is null)
        {
            return "Backup metadata is missing or invalid.";
        }

        if (string.IsNullOrWhiteSpace(metadata.BackupId) || !metadata.BackupId.Equals(backupId, StringComparison.OrdinalIgnoreCase))
        {
            return "Backup metadata has an invalid backup id.";
        }

        if (metadata.Files.Count != 1)
        {
            return "Backup metadata must contain exactly one workflow file for v1 undo.";
        }

        var file = metadata.Files[0];
        if (string.IsNullOrWhiteSpace(file.WorkflowPath)
            || string.IsNullOrWhiteSpace(file.OriginalHash)
            || string.IsNullOrWhiteSpace(file.ModifiedHash)
            || string.IsNullOrWhiteSpace(file.AppliedRuleId))
        {
            return "Backup metadata is missing required fields.";
        }

        return null;
    }

    private static UiPathBackupSummary Summary(
        string backupId,
        UiPathBackupMetadata metadata,
        UiPathBackupMetadataFile file,
        UiPathBackupStatus status,
        bool canUndo,
        string? reason)
    {
        return new UiPathBackupSummary
        {
            BackupId = backupId,
            CreatedAtUtc = metadata.CreatedAtUtc,
            WorkflowPath = file.WorkflowPath,
            RuleId = file.AppliedRuleId,
            PropertyName = file.PropertyName,
            PreviousValue = file.PreviousValue,
            NewValue = file.NewValue,
            OriginalHash = file.OriginalHash,
            ModifiedHash = file.ModifiedHash,
            Status = status,
            CanUndo = canUndo,
            Reason = reason
        };
    }
}

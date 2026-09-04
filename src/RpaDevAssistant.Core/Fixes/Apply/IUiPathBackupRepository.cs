namespace RpaDevAssistant.Core.Fixes.Apply;

public interface IUiPathBackupRepository
{
    IReadOnlyList<UiPathBackupSummary> ListBackups(string projectPath);

    UiPathBackupDetail GetBackup(string projectPath, string backupId);

    string ResolveBackedUpWorkflowPath(string projectPath, string backupId, string workflowPath);
}

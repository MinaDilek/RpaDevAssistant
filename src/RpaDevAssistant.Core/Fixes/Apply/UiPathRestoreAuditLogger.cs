using System.Text.Json;

namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed class UiPathRestoreAuditLogger : IUiPathRestoreAuditLogger
{
    public void LogSuccess(UiPathUndoResult result, string projectPath)
    {
        if (!result.Success || !result.Restored)
        {
            return;
        }

        var logsPath = Path.Combine(Path.GetFullPath(projectPath), ".rpadevassistant", "logs");
        Directory.CreateDirectory(logsPath);
        var line = JsonSerializer.Serialize(new
        {
            operationId = result.OperationId,
            operationType = "Undo",
            timestamp = result.RestoredAtUtc,
            backupId = result.BackupId,
            workflow = result.WorkflowPath,
            previousHash = result.PreviousHash,
            restoredHash = result.RestoredHash,
            safetyBackupId = result.SafetyBackupId,
            status = "Succeeded"
        });
        File.AppendAllText(Path.Combine(logsPath, "restores.jsonl"), line + Environment.NewLine);
    }
}

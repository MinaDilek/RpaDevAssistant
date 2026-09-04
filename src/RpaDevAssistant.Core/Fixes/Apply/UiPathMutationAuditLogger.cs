using System.Text.Json;

namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed class UiPathMutationAuditLogger : IUiPathMutationAuditLogger
{
    public void LogSuccess(UiPathFixApplyResult result)
    {
        if (!result.Success || !result.Applied || string.IsNullOrWhiteSpace(result.BackupPath))
        {
            return;
        }

        var projectRoot = FindProjectRootFromBackupPath(result.BackupPath);
        if (projectRoot is null)
        {
            return;
        }

        var logsPath = Path.Combine(projectRoot, ".rpadevassistant", "logs");
        Directory.CreateDirectory(logsPath);
        var line = JsonSerializer.Serialize(new
        {
            operationId = result.OperationId,
            operationType = "Apply",
            timestamp = result.AppliedAtUtc,
            workflow = result.WorkflowPath,
            rule = result.RuleId,
            property = result.PropertyName,
            oldValue = result.PreviousValue,
            newValue = result.NewValue,
            backupId = result.BackupId
        });
        File.AppendAllText(Path.Combine(logsPath, "mutations.jsonl"), line + Environment.NewLine);
    }

    private static string? FindProjectRootFromBackupPath(string backupPath)
    {
        var marker = $"{Path.DirectorySeparatorChar}.rpadevassistant{Path.DirectorySeparatorChar}";
        var index = backupPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index <= 0 ? null : backupPath[..index];
    }
}

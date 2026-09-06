namespace RpaDevAssistant.Core.Fixes.Apply;

public interface IUiPathBackupService
{
    UiPathBackupResult CreateBackup(
        string projectPath,
        string workflowPath,
        string workflowFullPath,
        string originalHash,
        string modifiedHash,
        string ruleId,
        string propertyName,
        string? previousValue,
        string? newValue,
        string? operationType = null,
        string? conversionPlanVersion = null);
}

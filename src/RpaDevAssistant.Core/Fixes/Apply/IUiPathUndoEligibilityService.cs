namespace RpaDevAssistant.Core.Fixes.Apply;

public interface IUiPathUndoEligibilityService
{
    UiPathBackupSummary Evaluate(string projectPath, string backupId, UiPathBackupMetadata? metadata);
}

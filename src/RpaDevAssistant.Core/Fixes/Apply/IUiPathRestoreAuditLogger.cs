namespace RpaDevAssistant.Core.Fixes.Apply;

public interface IUiPathRestoreAuditLogger
{
    void LogSuccess(UiPathUndoResult result, string projectPath);
}

namespace RpaDevAssistant.Core.Fixes.Apply;

public interface IUiPathMutationAuditLogger
{
    void LogSuccess(UiPathFixApplyResult result);
}

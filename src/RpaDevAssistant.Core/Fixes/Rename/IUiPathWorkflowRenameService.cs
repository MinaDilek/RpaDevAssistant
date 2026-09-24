namespace RpaDevAssistant.Core.Fixes.Rename;

public interface IUiPathWorkflowRenameService
{
    Task<UiPathWorkflowRenameResult> RenameAsync(UiPathWorkflowRenameRequest request, CancellationToken cancellationToken);
}

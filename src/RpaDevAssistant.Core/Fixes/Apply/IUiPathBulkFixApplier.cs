namespace RpaDevAssistant.Core.Fixes.Apply;

public interface IUiPathBulkFixApplier
{
    Task<UiPathBulkFixApplyResult> ApplyAllAsync(UiPathBulkFixApplyRequest request, CancellationToken cancellationToken);
}

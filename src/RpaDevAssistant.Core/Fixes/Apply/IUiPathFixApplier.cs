namespace RpaDevAssistant.Core.Fixes.Apply;

public interface IUiPathFixApplier
{
    Task<UiPathFixApplyResult> ApplyAsync(UiPathFixApplyRequest request, CancellationToken cancellationToken);
}

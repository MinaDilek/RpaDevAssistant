namespace RpaDevAssistant.Core.Fixes.Apply;

public interface IUiPathUndoService
{
    Task<UiPathUndoResult> UndoAsync(UiPathUndoRequest request, CancellationToken cancellationToken);
}

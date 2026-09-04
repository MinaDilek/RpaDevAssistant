namespace RpaDevAssistant.Core.Fixes.Apply;

public interface IUiPathBackupRestoreService
{
    Task<UiPathUndoResult> RestoreAsync(UiPathUndoRequest request, UiPathBackupMetadata metadata, CancellationToken cancellationToken);
}

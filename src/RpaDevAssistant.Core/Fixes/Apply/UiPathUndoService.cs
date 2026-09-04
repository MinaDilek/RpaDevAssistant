namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed class UiPathUndoService : IUiPathUndoService
{
    private readonly IUiPathBackupRepository backupRepository;
    private readonly IUiPathBackupRestoreService restoreService;
    private readonly IUiPathMutationLock mutationLock;

    public UiPathUndoService(
        IUiPathBackupRepository backupRepository,
        IUiPathBackupRestoreService restoreService,
        IUiPathMutationLock mutationLock)
    {
        this.backupRepository = backupRepository;
        this.restoreService = restoreService;
        this.mutationLock = mutationLock;
    }

    public async Task<UiPathUndoResult> UndoAsync(UiPathUndoRequest request, CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(request.ProjectPath) || !Directory.Exists(request.ProjectPath))
        {
            return Failed(request, "ProjectPath must point to an existing folder.", "INVALID_PROJECT", operationId);
        }

        if (string.IsNullOrWhiteSpace(request.BackupId) || string.IsNullOrWhiteSpace(request.WorkflowPath))
        {
            return Failed(request, "BackupId and WorkflowPath are required.", "INVALID_REQUEST", operationId);
        }

        var workflowPath = UiPathPathSafety.NormalizeRelativePath(request.WorkflowPath);
        await using var lease = await mutationLock.AcquireAsync(request.ProjectPath, workflowPath, cancellationToken).ConfigureAwait(false);

        var detail = backupRepository.GetBackup(request.ProjectPath, request.BackupId);
        if (detail.Summary is null || detail.Metadata is null)
        {
            return Failed(request, "Backup was not found.", "BACKUP_NOT_FOUND", operationId);
        }

        if (detail.Summary.Status == UiPathBackupStatus.AlreadyRestored)
        {
            return new UiPathUndoResult
            {
                Success = true,
                Restored = false,
                OperationId = operationId,
                Message = "No restore was necessary. Workflow is already in the state represented by this backup.",
                BackupId = request.BackupId,
                WorkflowPath = workflowPath,
                PreviousHash = detail.Summary.OriginalHash,
                RestoredHash = detail.Summary.OriginalHash,
                RequiresReanalysis = false
            };
        }

        if (!detail.Summary.CanUndo)
        {
            return Failed(request, detail.Summary.Reason ?? "Backup is not eligible for undo.", ErrorCodeForStatus(detail.Summary.Status), operationId);
        }

        if (!workflowPath.Equals(detail.Summary.WorkflowPath, StringComparison.OrdinalIgnoreCase))
        {
            return Failed(request, "Requested workflow does not match backup metadata.", "BACKUP_WORKFLOW_MISMATCH", operationId);
        }

        return await restoreService.RestoreAsync(request with { WorkflowPath = workflowPath }, detail.Metadata, cancellationToken).ConfigureAwait(false);
    }

    private static UiPathUndoResult Failed(UiPathUndoRequest request, string message, string errorCode, string operationId)
    {
        return new UiPathUndoResult
        {
            Success = false,
            Restored = false,
            OperationId = operationId,
            Message = message,
            BackupId = request.BackupId,
            WorkflowPath = request.WorkflowPath,
            ErrorCode = errorCode,
            ValidationResult = new UiPathFixApplyValidationResult { Errors = [message] }
        };
    }

    private static string ErrorCodeForStatus(UiPathBackupStatus status)
    {
        return status switch
        {
            UiPathBackupStatus.CurrentFileChanged => "UNDO_FILE_CHANGED",
            UiPathBackupStatus.MissingFile => "UNDO_TARGET_MISSING",
            UiPathBackupStatus.InvalidBackup => "BACKUP_INTEGRITY_FAILED",
            _ => status.ToString().ToUpperInvariant()
        };
    }
}

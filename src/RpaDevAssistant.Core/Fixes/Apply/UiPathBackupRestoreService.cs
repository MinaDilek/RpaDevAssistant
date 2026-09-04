using RpaDevAssistant.Core.Parsing;
using RpaDevAssistant.Core.Scanning;

namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed class UiPathBackupRestoreService : IUiPathBackupRestoreService
{
    private readonly IUiPathBackupRepository backupRepository;
    private readonly IUiPathXamlParser xamlParser;
    private readonly IUiPathProjectScanner scanner;
    private readonly IUiPathRestoreAuditLogger auditLogger;

    public UiPathBackupRestoreService(
        IUiPathBackupRepository backupRepository,
        IUiPathXamlParser xamlParser,
        IUiPathProjectScanner scanner,
        IUiPathRestoreAuditLogger auditLogger)
    {
        this.backupRepository = backupRepository;
        this.xamlParser = xamlParser;
        this.scanner = scanner;
        this.auditLogger = auditLogger;
    }

    public async Task<UiPathUndoResult> RestoreAsync(UiPathUndoRequest request, UiPathBackupMetadata metadata, CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var projectPath = Path.GetFullPath(request.ProjectPath);
        var workflowPath = UiPathPathSafety.NormalizeRelativePath(request.WorkflowPath);
        var file = metadata.Files.Single();
        var currentPath = UiPathPathSafety.ResolveProjectFile(projectPath, workflowPath);
        if (currentPath is null)
        {
            return Failed(request, "Workflow path is unsafe.", "PATH_TRAVERSAL", operationId: operationId);
        }

        if (!File.Exists(currentPath))
        {
            return Failed(request, "The workflow targeted by this backup no longer exists.", "UNDO_TARGET_MISSING", operationId: operationId);
        }

        var backupPath = backupRepository.ResolveBackedUpWorkflowPath(projectPath, request.BackupId, workflowPath);
        if (!File.Exists(backupPath))
        {
            return Failed(request, "Backed-up workflow file is missing.", "BACKUP_INTEGRITY_FAILED", operationId: operationId);
        }

        var backupHash = UiPathFileHash.Sha256(backupPath);
        if (!string.Equals(backupHash, file.OriginalHash, StringComparison.OrdinalIgnoreCase))
        {
            return Failed(request, "Backup integrity validation failed.", "BACKUP_INTEGRITY_FAILED", operationId: operationId);
        }

        var currentHash = UiPathFileHash.Sha256(currentPath);
        if (!string.IsNullOrWhiteSpace(request.ExpectedCurrentHash)
            && !string.Equals(request.ExpectedCurrentHash, currentHash, StringComparison.OrdinalIgnoreCase))
        {
            return Failed(request, "The workflow has changed since this undo action was prepared. Automatic undo was blocked to prevent data loss.", "UNDO_FILE_CHANGED", currentHash, operationId: operationId);
        }

        if (string.Equals(currentHash, file.OriginalHash, StringComparison.OrdinalIgnoreCase))
        {
            return new UiPathUndoResult
            {
                Success = true,
                Restored = false,
                OperationId = operationId,
                Message = "No restore was necessary. Workflow is already in the state represented by this backup.",
                BackupId = request.BackupId,
                WorkflowPath = workflowPath,
                PreviousHash = currentHash,
                RestoredHash = currentHash,
                RequiresReanalysis = false
            };
        }

        if (!string.Equals(currentHash, file.ModifiedHash, StringComparison.OrdinalIgnoreCase))
        {
            return Failed(request, "The workflow has changed since this fix was applied. Automatic undo was blocked to prevent data loss.", "UNDO_FILE_CHANGED", currentHash, operationId: operationId);
        }

        var backupParse = xamlParser.Parse(backupPath, projectPath);
        if (backupParse.ParseErrors.Count > 0)
        {
            return Failed(request, "The backup XAML could not be parsed and was not restored.", "BACKUP_INTEGRITY_FAILED", currentHash, operationId: operationId);
        }

        var safety = request.CreateSafetyBackup
            ? CreateSafetyBackup(projectPath, request.BackupId, workflowPath, currentPath, currentHash)
            : null;
        var tempPath = Path.Combine(Path.GetDirectoryName(currentPath)!, $".{Path.GetFileName(currentPath)}.{Guid.NewGuid():N}.restore.tmp");

        try
        {
            await File.WriteAllBytesAsync(tempPath, await File.ReadAllBytesAsync(backupPath, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            _ = xamlParser.Parse(tempPath, projectPath);
            File.Move(tempPath, currentPath, overwrite: true);
        }
        catch (IOException)
        {
            TryDelete(tempPath);
            return Failed(request, "The workflow file is currently in use and could not be restored.", "FILE_LOCKED", currentHash, safety?.SafetyBackupId, operationId);
        }
        catch (UnauthorizedAccessException)
        {
            TryDelete(tempPath);
            return Failed(request, "The workflow file could not be accessed for restore.", "FILE_ACCESS_DENIED", currentHash, safety?.SafetyBackupId, operationId);
        }

        var postValidation = ValidatePostRestore(projectPath, workflowPath, currentPath, file.OriginalHash);
        if (!postValidation.IsValid)
        {
            RestoreSafetyBackup(safety?.BackupFilePath, currentPath);
            return new UiPathUndoResult
            {
                Success = false,
                Restored = false,
                OperationId = operationId,
                Message = "Undo failed. Current modified version restored from safety backup.",
                BackupId = request.BackupId,
                WorkflowPath = workflowPath,
                PreviousHash = currentHash,
                RestoredHash = SafeHash(currentPath),
                SafetyBackupId = safety?.SafetyBackupId,
                RequiresReanalysis = true,
                ErrorCode = "POST_RESTORE_VALIDATION_FAILED",
                ValidationResult = postValidation
            };
        }

        var result = new UiPathUndoResult
        {
            Success = true,
            Restored = true,
            OperationId = operationId,
            Message = "Change restored successfully.",
            BackupId = request.BackupId,
            WorkflowPath = workflowPath,
            PreviousHash = currentHash,
            RestoredHash = UiPathFileHash.Sha256(currentPath),
            SafetyBackupId = safety?.SafetyBackupId,
            RestoredAtUtc = DateTimeOffset.UtcNow,
            RequiresReanalysis = true,
            ValidationResult = postValidation
        };
        auditLogger.LogSuccess(result, projectPath);
        return result;
    }

    private UiPathFixApplyValidationResult ValidatePostRestore(string projectPath, string workflowPath, string currentPath, string expectedHash)
    {
        var result = new UiPathFixApplyValidationResult();
        if (!string.Equals(UiPathFileHash.Sha256(currentPath), expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add("Restored workflow hash does not match original hash.");
        }

        if (xamlParser.Parse(currentPath, projectPath).ParseErrors.Count > 0)
        {
            result.Errors.Add("Restored XAML could not be parsed.");
        }

        if (scanner.Scan(projectPath).Workflows.All(item => !item.RelativePath.Equals(workflowPath, StringComparison.OrdinalIgnoreCase)))
        {
            result.Errors.Add("Project scanner could not read the restored workflow.");
        }

        return result;
    }

    private static UiPathSafetyBackupResult CreateSafetyBackup(string projectPath, string sourceBackupId, string workflowPath, string currentPath, string currentHash)
    {
        var safetyBackupId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var root = Path.Combine(projectPath, ".rpadevassistant", "restore-backups", safetyBackupId);
        var backupPath = Path.Combine(root, workflowPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        File.Copy(currentPath, backupPath, overwrite: false);

        var metadata = new UiPathSafetyBackupMetadata
        {
            SafetyBackupId = safetyBackupId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            SourceBackupId = sourceBackupId,
            WorkflowPath = workflowPath,
            CurrentHash = currentHash
        };
        File.WriteAllText(Path.Combine(root, "restore-backup.json"), System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        return new UiPathSafetyBackupResult
        {
            SafetyBackupId = safetyBackupId,
            BackupFilePath = backupPath,
            BackupRootPath = root
        };
    }

    private static UiPathUndoResult Failed(UiPathUndoRequest request, string message, string errorCode, string? previousHash = null, string? safetyBackupId = null, string? operationId = null)
    {
        return new UiPathUndoResult
        {
            Success = false,
            Restored = false,
            OperationId = operationId,
            Message = message,
            BackupId = request.BackupId,
            WorkflowPath = UiPathPathSafety.NormalizeRelativePath(request.WorkflowPath),
            PreviousHash = previousHash,
            SafetyBackupId = safetyBackupId,
            ErrorCode = errorCode,
            ValidationResult = new UiPathFixApplyValidationResult { Errors = [message] }
        };
    }

    private static void RestoreSafetyBackup(string? safetyBackupPath, string currentPath)
    {
        if (!string.IsNullOrWhiteSpace(safetyBackupPath) && File.Exists(safetyBackupPath))
        {
            File.Copy(safetyBackupPath, currentPath, overwrite: true);
        }
    }

    private static string? SafeHash(string path)
    {
        try
        {
            return File.Exists(path) ? UiPathFileHash.Sha256(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}

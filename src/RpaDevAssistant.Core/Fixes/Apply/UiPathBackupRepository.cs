using System.Text.Json;

namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed class UiPathBackupRepository : IUiPathBackupRepository
{
    private readonly IUiPathUndoEligibilityService eligibilityService;

    public UiPathBackupRepository(IUiPathUndoEligibilityService eligibilityService)
    {
        this.eligibilityService = eligibilityService;
    }

    public IReadOnlyList<UiPathBackupSummary> ListBackups(string projectPath)
    {
        var backupRoot = Path.Combine(Path.GetFullPath(projectPath), ".rpadevassistant", "backups");
        if (!Directory.Exists(backupRoot))
        {
            return [];
        }

        return Directory.EnumerateDirectories(backupRoot)
            .Select(path => GetBackup(projectPath, Path.GetFileName(path)).Summary)
            .Where(summary => summary is not null)
            .Cast<UiPathBackupSummary>()
            .OrderByDescending(summary => summary.CreatedAtUtc ?? DateTimeOffset.MinValue)
            .ThenByDescending(summary => summary.BackupId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public UiPathBackupDetail GetBackup(string projectPath, string backupId)
    {
        if (!IsSafeBackupId(backupId))
        {
            return new UiPathBackupDetail
            {
                Summary = new UiPathBackupSummary
                {
                    BackupId = backupId,
                    Status = UiPathBackupStatus.InvalidBackup,
                    CanUndo = false,
                    Reason = "Backup id is invalid."
                }
            };
        }

        var root = Path.Combine(Path.GetFullPath(projectPath), ".rpadevassistant", "backups", backupId);
        if (!Directory.Exists(root))
        {
            return new UiPathBackupDetail();
        }

        var metadata = ReadMetadata(Path.Combine(root, "backup.json"));
        return new UiPathBackupDetail
        {
            Metadata = metadata,
            Summary = eligibilityService.Evaluate(Path.GetFullPath(projectPath), backupId, metadata)
        };
    }

    public string ResolveBackedUpWorkflowPath(string projectPath, string backupId, string workflowPath)
    {
        if (!IsSafeBackupId(backupId))
        {
            throw new InvalidOperationException("Backup id is invalid.");
        }

        var backupRoot = Path.Combine(Path.GetFullPath(projectPath), ".rpadevassistant", "backups", backupId);
        var resolved = UiPathPathSafety.ResolveProjectFile(backupRoot, workflowPath)
            ?? throw new InvalidOperationException("Backed-up workflow path is unsafe.");
        return resolved;
    }

    private static UiPathBackupMetadata? ReadMetadata(string metadataPath)
    {
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<UiPathBackupMetadata>(File.ReadAllText(metadataPath), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool IsSafeBackupId(string backupId)
    {
        return !string.IsNullOrWhiteSpace(backupId)
            && backupId.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            && !backupId.Contains("..", StringComparison.Ordinal);
    }
}

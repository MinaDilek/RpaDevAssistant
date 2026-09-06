using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.History;

public sealed class UiPathAnalysisHistoryOptions
{
    public int MaxSnapshotsPerProject { get; init; } = 20;

    public string? StorageRoot { get; init; }
}

public sealed class UiPathAnalysisHistoryService : IUiPathAnalysisHistoryService
{
    private const string HistoryFolderName = "analysis-history";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly UiPathAnalysisHistoryOptions options;

    public UiPathAnalysisHistoryService(UiPathAnalysisHistoryOptions? options = null)
    {
        this.options = options ?? new UiPathAnalysisHistoryOptions();
    }

    public UiPathAnalysisSnapshotSaveResult SaveSnapshot(UiPathProjectAnalysisResult analysis)
    {
        var snapshots = LoadSnapshots(analysis.ProjectPath);
        var previous = snapshots.FirstOrDefault();
        var snapshot = CreateSnapshot(analysis);
        var duplicate = previous is not null && string.Equals(previous.ResultHash, snapshot.ResultHash, StringComparison.OrdinalIgnoreCase);
        if (duplicate)
        {
            return new UiPathAnalysisSnapshotSaveResult
            {
                Snapshot = previous!,
                Saved = false,
                DuplicateSkipped = true,
                ComparisonWithPrevious = snapshots.Count > 1 ? CompareSnapshots(snapshots[1], previous!) : null
            };
        }

        WriteSnapshot(snapshot);
        PruneSnapshots(snapshot.ProjectIdentity.ProjectId);

        return new UiPathAnalysisSnapshotSaveResult
        {
            Snapshot = snapshot,
            Saved = true,
            DuplicateSkipped = false,
            ComparisonWithPrevious = previous is null ? null : CompareSnapshots(previous, snapshot)
        };
    }

    public UiPathAnalysisHistoryList ListSnapshots(string projectPath)
    {
        var snapshots = LoadSnapshots(projectPath);
        return ToHistoryList(snapshots);
    }

    public UiPathAnalysisHistoryList ListSnapshots()
    {
        return ToHistoryList(LoadAllSnapshots());
    }

    private static UiPathAnalysisHistoryList ToHistoryList(IReadOnlyList<UiPathAnalysisSnapshot> snapshots)
    {
        var summaries = snapshots
            .Select((snapshot, index) =>
            {
                var previous = snapshots
                    .Skip(index + 1)
                    .FirstOrDefault(candidate => string.Equals(candidate.ProjectIdentity.ProjectId, snapshot.ProjectIdentity.ProjectId, StringComparison.OrdinalIgnoreCase));
                var comparison = previous is null ? null : CompareSnapshots(previous, snapshot);
                return new UiPathAnalysisSnapshotSummary
                {
                    SnapshotId = snapshot.SnapshotId,
                    GeneratedAtUtc = snapshot.GeneratedAtUtc,
                    ProjectName = snapshot.ProjectName,
                    ProjectPath = snapshot.ProjectIdentity.ProjectPath,
                    Score = snapshot.Score,
                    Grade = snapshot.Grade,
                    WorkflowCount = snapshot.WorkflowCount,
                    TotalActivityCount = snapshot.TotalActivityCount,
                    TotalFindings = snapshot.FindingSummary.Total,
                    PreviousSnapshotId = previous?.SnapshotId,
                    ScoreDelta = comparison?.ScoreDelta,
                    TotalFindingDelta = comparison?.TotalFindingDelta,
                    NewFindingCount = comparison?.NewFindings.Count,
                    ResolvedFindingCount = comparison?.ResolvedFindings.Count
                };
            })
            .ToArray();

        return new UiPathAnalysisHistoryList { Snapshots = summaries };
    }

    public UiPathAnalysisComparison? Compare(string projectPath, string baselineSnapshotId, string targetSnapshotId)
    {
        var snapshots = LoadSnapshots(projectPath);
        var baseline = snapshots.FirstOrDefault(snapshot => snapshot.SnapshotId.Equals(baselineSnapshotId, StringComparison.OrdinalIgnoreCase));
        var target = snapshots.FirstOrDefault(snapshot => snapshot.SnapshotId.Equals(targetSnapshotId, StringComparison.OrdinalIgnoreCase));
        return baseline is null || target is null ? null : CompareSnapshots(baseline, target);
    }

    public UiPathAnalysisComparison? CompareLatestWithPrevious(string projectPath)
    {
        var snapshots = LoadSnapshots(projectPath);
        return snapshots.Count < 2 ? null : CompareSnapshots(snapshots[1], snapshots[0]);
    }

    private UiPathAnalysisSnapshot CreateSnapshot(UiPathProjectAnalysisResult analysis)
    {
        var identity = CreateProjectIdentity(analysis.ProjectScan);
        var findings = analysis.Analysis.Findings
            .Select(ToFindingSnapshot)
            .OrderBy(finding => finding.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.ContentHash, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var workflows = analysis.ProjectScan.Workflows
            .Select(workflow => ToWorkflowSnapshot(workflow, findings))
            .OrderBy(workflow => workflow.WorkflowPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var resultHash = Hash(string.Join("|", [
            analysis.QualityScore.Score.ToString(System.Globalization.CultureInfo.InvariantCulture),
            analysis.QualityScore.Grade,
            analysis.WorkflowCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            analysis.TotalActivityCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            string.Join(",", findings.Select(finding => finding.ContentHash)),
            string.Join(",", workflows.Select(workflow => $"{workflow.WorkflowPath}:{workflow.ActivityCount}:{workflow.FindingCount}:{workflow.ComplexityScore}:{workflow.ComplexityLevel}"))
        ]));

        return new UiPathAnalysisSnapshot
        {
            SnapshotId = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{resultHash[..8]}",
            ProjectIdentity = identity,
            ProjectName = analysis.ProjectName,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Score = analysis.QualityScore.Score,
            Grade = analysis.QualityScore.Grade,
            WorkflowCount = analysis.WorkflowCount,
            TotalActivityCount = analysis.TotalActivityCount,
            FindingSummary = SeverityCounts(analysis.Analysis.Findings),
            Findings = findings,
            Workflows = workflows,
            ResultHash = resultHash
        };
    }

    private static UiPathFindingSnapshot ToFindingSnapshot(UiPathAnalysisFinding finding)
    {
        var workflowPath = NormalizeWorkflowPath(finding.WorkflowPath);
        var stableLocator = finding.Scope == UiPathFindingScope.Aggregated
            ? $"aggregate:{finding.RuleId}:{workflowPath}"
            : string.Join(":", [
                finding.ActivityName ?? string.Empty,
                finding.ActivityDisplayName ?? string.Empty,
                finding.PropertyName ?? string.Empty,
                finding.CurrentValue ?? string.Empty
            ]);
        var id = Hash(string.Join("|", finding.RuleId, workflowPath, stableLocator));
        var contentHash = Hash(string.Join("|", id, finding.Severity, finding.Category, finding.Message, finding.OccurrenceCount.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        return new UiPathFindingSnapshot
        {
            Id = id,
            ContentHash = contentHash,
            RuleId = finding.RuleId,
            RuleName = finding.RuleName,
            Severity = finding.Severity,
            Category = finding.Category,
            WorkflowPath = workflowPath,
            ActivityName = finding.ActivityName,
            ActivityDisplayName = finding.ActivityDisplayName,
            PropertyName = finding.PropertyName,
            Message = finding.Message,
            OccurrenceCount = Math.Max(1, finding.OccurrenceCount)
        };
    }

    private static UiPathWorkflowSnapshot ToWorkflowSnapshot(UiPathWorkflowInfo workflow, IReadOnlyList<UiPathFindingSnapshot> findings)
    {
        var normalizedPath = NormalizeWorkflowPath(workflow.RelativePath) ?? workflow.RelativePath;
        var complexity = workflow.Analysis?.Complexity;
        return new UiPathWorkflowSnapshot
        {
            WorkflowPath = normalizedPath,
            ActivityCount = workflow.ActivityCount,
            FindingCount = findings.Count(finding => string.Equals(finding.WorkflowPath, normalizedPath, StringComparison.OrdinalIgnoreCase)),
            ComplexityScore = complexity?.ComplexityScore,
            ComplexityLevel = complexity?.ComplexityLevel.ToString()
        };
    }

    private static UiPathHistorySeverityCounts SeverityCounts(IReadOnlyCollection<UiPathAnalysisFinding> findings)
    {
        return new UiPathHistorySeverityCounts
        {
            Total = findings.Count,
            Critical = findings.Count(finding => finding.Severity == RuleSeverity.Critical),
            Error = findings.Count(finding => finding.Severity == RuleSeverity.Error),
            Warning = findings.Count(finding => finding.Severity == RuleSeverity.Warning),
            Suggestion = findings.Count(finding => finding.Severity == RuleSeverity.Suggestion),
            Info = findings.Count(finding => finding.Severity == RuleSeverity.Info)
        };
    }

    private static UiPathHistorySeverityCounts Delta(UiPathHistorySeverityCounts before, UiPathHistorySeverityCounts after)
    {
        return new UiPathHistorySeverityCounts
        {
            Total = after.Total - before.Total,
            Critical = after.Critical - before.Critical,
            Error = after.Error - before.Error,
            Warning = after.Warning - before.Warning,
            Suggestion = after.Suggestion - before.Suggestion,
            Info = after.Info - before.Info
        };
    }

    private static UiPathAnalysisComparison CompareSnapshots(UiPathAnalysisSnapshot baseline, UiPathAnalysisSnapshot target)
    {
        var before = baseline.Findings.GroupBy(finding => finding.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var after = target.Findings.GroupBy(finding => finding.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var newFindings = after.Where(pair => !before.ContainsKey(pair.Key)).Select(pair => Compared(UiPathFindingComparisonState.New, pair.Value)).ToArray();
        var resolvedFindings = before.Where(pair => !after.ContainsKey(pair.Key)).Select(pair => Compared(UiPathFindingComparisonState.Resolved, pair.Value)).ToArray();
        var unchangedFindings = after
            .Where(pair => before.TryGetValue(pair.Key, out var previous) && previous.ContentHash.Equals(pair.Value.ContentHash, StringComparison.OrdinalIgnoreCase))
            .Select(pair => Compared(UiPathFindingComparisonState.Unchanged, pair.Value))
            .ToArray();
        var changedFindings = after
            .Where(pair => before.TryGetValue(pair.Key, out var previous) && !previous.ContentHash.Equals(pair.Value.ContentHash, StringComparison.OrdinalIgnoreCase))
            .Select(pair => Compared(UiPathFindingComparisonState.Changed, pair.Value))
            .ToArray();

        return new UiPathAnalysisComparison
        {
            BaselineSnapshotId = baseline.SnapshotId,
            TargetSnapshotId = target.SnapshotId,
            ScoreDelta = target.Score - baseline.Score,
            GradeBefore = baseline.Grade,
            GradeAfter = target.Grade,
            TotalFindingDelta = target.FindingSummary.Total - baseline.FindingSummary.Total,
            SeverityBefore = baseline.FindingSummary,
            SeverityAfter = target.FindingSummary,
            SeverityDelta = Delta(baseline.FindingSummary, target.FindingSummary),
            WorkflowCountDelta = target.WorkflowCount - baseline.WorkflowCount,
            ActivityCountDelta = target.TotalActivityCount - baseline.TotalActivityCount,
            NewFindings = newFindings,
            ResolvedFindings = resolvedFindings,
            UnchangedFindings = unchangedFindings,
            ChangedFindings = changedFindings,
            WorkflowChanges = CompareWorkflows(baseline, target, newFindings, resolvedFindings)
        };
    }

    private static IReadOnlyList<UiPathWorkflowComparison> CompareWorkflows(
        UiPathAnalysisSnapshot baseline,
        UiPathAnalysisSnapshot target,
        IReadOnlyList<UiPathComparedFinding> newFindings,
        IReadOnlyList<UiPathComparedFinding> resolvedFindings)
    {
        var paths = baseline.Workflows.Select(workflow => workflow.WorkflowPath)
            .Concat(target.Workflows.Select(workflow => workflow.WorkflowPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        return paths
            .Select(path =>
            {
                var before = baseline.Workflows.FirstOrDefault(workflow => workflow.WorkflowPath.Equals(path, StringComparison.OrdinalIgnoreCase));
                var after = target.Workflows.FirstOrDefault(workflow => workflow.WorkflowPath.Equals(path, StringComparison.OrdinalIgnoreCase));
                return new UiPathWorkflowComparison
                {
                    WorkflowPath = path,
                    ActivityCountDelta = (after?.ActivityCount ?? 0) - (before?.ActivityCount ?? 0),
                    FindingCountDelta = (after?.FindingCount ?? 0) - (before?.FindingCount ?? 0),
                    ComplexityScoreDelta = after?.ComplexityScore is null && before?.ComplexityScore is null ? null : (after?.ComplexityScore ?? 0) - (before?.ComplexityScore ?? 0),
                    NewFindingCount = newFindings.Count(finding => finding.Finding.WorkflowPath?.Equals(path, StringComparison.OrdinalIgnoreCase) == true),
                    ResolvedFindingCount = resolvedFindings.Count(finding => finding.Finding.WorkflowPath?.Equals(path, StringComparison.OrdinalIgnoreCase) == true)
                };
            })
            .Where(change => change.ActivityCountDelta != 0 || change.FindingCountDelta != 0 || change.ComplexityScoreDelta != 0 || change.NewFindingCount != 0 || change.ResolvedFindingCount != 0)
            .ToArray();
    }

    private static UiPathComparedFinding Compared(UiPathFindingComparisonState state, UiPathFindingSnapshot finding)
    {
        return new UiPathComparedFinding { State = state, Finding = finding };
    }

    private IReadOnlyList<UiPathAnalysisSnapshot> LoadSnapshots(string projectPath)
    {
        var identity = CreateProjectIdentity(projectPath, null);
        var directory = ProjectDirectory(identity.ProjectId);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var snapshots = new List<UiPathAnalysisSnapshot>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<UiPathAnalysisSnapshot>(File.ReadAllText(file), JsonOptions);
                if (snapshot is not null)
                {
                    snapshots.Add(snapshot);
                }
            }
            catch
            {
                // Corrupt local history must not block project analysis.
            }
        }

        return snapshots
            .OrderByDescending(snapshot => snapshot.GeneratedAtUtc)
            .ThenByDescending(snapshot => snapshot.SnapshotId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<UiPathAnalysisSnapshot> LoadAllSnapshots()
    {
        var root = StorageRoot();
        if (!Directory.Exists(root))
        {
            return [];
        }

        var snapshots = new List<UiPathAnalysisSnapshot>();
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var snapshot = JsonSerializer.Deserialize<UiPathAnalysisSnapshot>(File.ReadAllText(file), JsonOptions);
                    if (snapshot is not null)
                    {
                        snapshots.Add(snapshot);
                    }
                }
                catch
                {
                    // Corrupt local history must not block the global history view.
                }
            }
        }

        return snapshots
            .OrderByDescending(snapshot => snapshot.GeneratedAtUtc)
            .ThenByDescending(snapshot => snapshot.SnapshotId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void WriteSnapshot(UiPathAnalysisSnapshot snapshot)
    {
        var directory = ProjectDirectory(snapshot.ProjectIdentity.ProjectId);
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, $"{snapshot.SnapshotId}.json");
        var temp = Path.Combine(directory, $"{snapshot.SnapshotId}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, JsonOptions), new UTF8Encoding(false));
        File.Move(temp, target, overwrite: true);
    }

    private void PruneSnapshots(string projectId)
    {
        var directory = ProjectDirectory(projectId);
        if (!Directory.Exists(directory))
        {
            return;
        }

        var files = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(file => new FileInfo(file))
            .OrderByDescending(file => file.CreationTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(Math.Max(1, options.MaxSnapshotsPerProject))
            .ToArray();
        foreach (var file in files)
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // Retention cleanup is best effort.
            }
        }
    }

    private string ProjectDirectory(string projectId)
    {
        return Path.Combine(StorageRoot(), projectId);
    }

    private string StorageRoot()
    {
        var configured = options.StorageRoot ?? Environment.GetEnvironmentVariable("RPADA_HISTORY_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        return Path.Combine(localAppData, "RPA Dev Assistant", HistoryFolderName);
    }

    private static UiPathProjectIdentity CreateProjectIdentity(ProjectScanResult scan)
    {
        return CreateProjectIdentity(scan.ProjectPath, scan.ProjectName);
    }

    private static UiPathProjectIdentity CreateProjectIdentity(string projectPath, string? projectName)
    {
        var fullPath = Path.GetFullPath(projectPath);
        var projectJson = Path.Combine(fullPath, "project.json");
        string? projectJsonHash = null;
        if (File.Exists(projectJson))
        {
            projectJsonHash = Hash(File.ReadAllBytes(projectJson));
        }

        var idSource = string.Join("|", projectJsonHash ?? string.Empty, NormalizeFilePath(fullPath));
        return new UiPathProjectIdentity
        {
            ProjectId = Hash(idSource),
            ProjectPath = fullPath,
            ProjectName = projectName,
            ProjectJsonHash = projectJsonHash
        };
    }

    private static string? NormalizeWorkflowPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : path.Replace('\\', '/').TrimStart('/');
    }

    private static string NormalizeFilePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/').ToUpperInvariant();
    }

    private static string Hash(string value)
    {
        return Hash(Encoding.UTF8.GetBytes(value));
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

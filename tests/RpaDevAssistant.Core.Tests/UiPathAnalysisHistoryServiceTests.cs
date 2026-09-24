using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.History;
using RpaDevAssistant.Core.Models;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathAnalysisHistoryServiceTests
{
    [Fact]
    public void SaveSnapshot_PersistsAndReloadsHistory()
    {
        using var directory = new TempHistoryDirectory();
        var service = directory.CreateService();

        var saved = service.SaveSnapshot(Analysis(directory.ProjectPath, [Finding("RPA001", "Main.xaml")], score: 90));
        var history = service.ListSnapshots(directory.ProjectPath);

        Assert.True(saved.Saved);
        var summary = Assert.Single(history.Snapshots);
        Assert.Equal(saved.Snapshot.SnapshotId, summary.SnapshotId);
        Assert.Equal(90, summary.Score);
        Assert.Equal(1, summary.TotalFindings);
        Assert.Equal(Path.GetFullPath(directory.ProjectPath), summary.ProjectPath);
    }

    [Fact]
    public void ListSnapshots_WithoutProjectPath_ReturnsAllProjectsNewestFirst()
    {
        using var directory = new TempHistoryDirectory();
        var service = directory.CreateService();
        var secondProjectPath = Path.Combine(directory.RootPath, "second-project");
        Directory.CreateDirectory(secondProjectPath);
        File.WriteAllText(Path.Combine(secondProjectPath, "project.json"), "{\"name\":\"SecondProject\"}");

        service.SaveSnapshot(Analysis(directory.ProjectPath, [Finding("RPA001", "Main.xaml")], score: 90, projectName: "FirstProject"));
        service.SaveSnapshot(Analysis(secondProjectPath, [Finding("RPA002", "Main.xaml")], score: 80, projectName: "SecondProject"));

        var history = service.ListSnapshots();

        Assert.Equal(2, history.Snapshots.Count);
        Assert.Contains(history.Snapshots, snapshot => snapshot.ProjectName == "FirstProject" && snapshot.ProjectPath == Path.GetFullPath(directory.ProjectPath));
        Assert.Contains(history.Snapshots, snapshot => snapshot.ProjectName == "SecondProject" && snapshot.ProjectPath == Path.GetFullPath(secondProjectPath));
    }

    [Fact]
    public void SaveSnapshot_SkipsDuplicateResult()
    {
        using var directory = new TempHistoryDirectory();
        var service = directory.CreateService();

        var first = service.SaveSnapshot(Analysis(directory.ProjectPath, [Finding("RPA001", "Main.xaml")]));
        var second = service.SaveSnapshot(Analysis(directory.ProjectPath, [Finding("RPA001", "Main.xaml")]));

        Assert.True(first.Saved);
        Assert.False(second.Saved);
        Assert.True(second.DuplicateSkipped);
        Assert.Single(service.ListSnapshots(directory.ProjectPath).Snapshots);
    }

    [Fact]
    public void SaveSnapshot_AppliesRetentionLimit()
    {
        using var directory = new TempHistoryDirectory(maxSnapshots: 2);
        var service = directory.CreateService();

        service.SaveSnapshot(Analysis(directory.ProjectPath, [Finding("RPA001", "Main.xaml")], score: 90));
        service.SaveSnapshot(Analysis(directory.ProjectPath, [Finding("RPA002", "Main.xaml")], score: 80));
        service.SaveSnapshot(Analysis(directory.ProjectPath, [Finding("RPA003", "Main.xaml")], score: 70));

        Assert.Equal(2, service.ListSnapshots(directory.ProjectPath).Snapshots.Count);
    }

    [Fact]
    public void Compare_DetectsNewResolvedUnchangedAndChangedFindings()
    {
        using var directory = new TempHistoryDirectory();
        var service = directory.CreateService();

        var baseline = service.SaveSnapshot(Analysis(directory.ProjectPath, [
            Finding("RPA001", "Main.xaml", message: "same"),
            Finding("RPA002", "Main.xaml", message: "resolved"),
            Finding("RPA003", "Framework/Process.xaml", message: "before")
        ], score: 80));
        var target = service.SaveSnapshot(Analysis(directory.ProjectPath, [
            Finding("RPA001", "Main.xaml", message: "same"),
            Finding("RPA003", "Framework/Process.xaml", message: "after"),
            Finding("RPA004", "Business/Login.xaml", message: "new")
        ], score: 85));

        var comparison = service.Compare(directory.ProjectPath, baseline.Snapshot.SnapshotId, target.Snapshot.SnapshotId);

        Assert.NotNull(comparison);
        Assert.Equal(5, comparison.ScoreDelta);
        Assert.Single(comparison.NewFindings);
        Assert.Single(comparison.ResolvedFindings);
        Assert.Single(comparison.UnchangedFindings);
        Assert.Single(comparison.ChangedFindings);
        Assert.Contains(comparison.WorkflowChanges, workflow => workflow.WorkflowPath == "Business/Login.xaml" && workflow.NewFindingCount == 1);
    }

    [Fact]
    public void Compare_FallsBackToSnapshotIdsWhenProjectPathLookupMisses()
    {
        using var directory = new TempHistoryDirectory();
        var service = directory.CreateService();

        var baseline = service.SaveSnapshot(Analysis(directory.ProjectPath, [Finding("RPA001", "Main.xaml")], score: 80));
        var target = service.SaveSnapshot(Analysis(directory.ProjectPath, [Finding("RPA002", "Main.xaml")], score: 85));

        var comparison = service.Compare(Path.Combine(directory.RootPath, "renamed-project"), baseline.Snapshot.SnapshotId, target.Snapshot.SnapshotId);

        Assert.NotNull(comparison);
        Assert.Equal(5, comparison.ScoreDelta);
        Assert.Single(comparison.NewFindings);
        Assert.Single(comparison.ResolvedFindings);
    }

    [Fact]
    public void Compare_DoesNotCrossCompareDifferentProjectsDuringFallback()
    {
        using var directory = new TempHistoryDirectory();
        var service = directory.CreateService();
        var secondProjectPath = Path.Combine(directory.RootPath, "second-project");
        Directory.CreateDirectory(secondProjectPath);
        File.WriteAllText(Path.Combine(secondProjectPath, "project.json"), "{\"name\":\"SecondProject\"}");

        var baseline = service.SaveSnapshot(Analysis(directory.ProjectPath, [Finding("RPA001", "Main.xaml")], score: 80, projectName: "FirstProject"));
        var target = service.SaveSnapshot(Analysis(secondProjectPath, [Finding("RPA002", "Main.xaml")], score: 85, projectName: "SecondProject"));

        var comparison = service.Compare(Path.Combine(directory.RootPath, "missing-project"), baseline.Snapshot.SnapshotId, target.Snapshot.SnapshotId);

        Assert.Null(comparison);
    }

    [Fact]
    public void FindingIdentity_DoesNotDependOnlyOnRuntimeActivityId()
    {
        using var directory = new TempHistoryDirectory();
        var service = directory.CreateService();

        var baseline = service.SaveSnapshot(Analysis(directory.ProjectPath, [Finding("RPA007", "Main.xaml", activityId: "runtime-1", activityName: "Click", displayName: "Click")]));
        var target = service.SaveSnapshot(Analysis(directory.ProjectPath, [Finding("RPA007", "Main.xaml", activityId: "runtime-2", activityName: "Click", displayName: "Click")]));

        var comparison = service.Compare(directory.ProjectPath, baseline.Snapshot.SnapshotId, target.Snapshot.SnapshotId);

        Assert.NotNull(comparison);
        Assert.Empty(comparison.NewFindings);
        Assert.Empty(comparison.ResolvedFindings);
        Assert.Single(comparison.UnchangedFindings);
    }

    [Fact]
    public void ListSnapshots_IgnoresCorruptedSnapshotFiles()
    {
        using var directory = new TempHistoryDirectory();
        var service = directory.CreateService();
        service.SaveSnapshot(Analysis(directory.ProjectPath, [Finding("RPA001", "Main.xaml")]));

        var snapshotId = service.ListSnapshots(directory.ProjectPath).Snapshots.Single().SnapshotId;
        var projectHistoryDirectory = Directory.GetDirectories(directory.HistoryRoot).Single();
        File.WriteAllText(Path.Combine(projectHistoryDirectory, "corrupt.json"), "{not-json");

        var history = service.ListSnapshots(directory.ProjectPath);

        Assert.Single(history.Snapshots);
        Assert.NotEqual(snapshotId, string.Empty);
    }

    private static UiPathProjectAnalysisResult Analysis(string projectPath, IReadOnlyList<UiPathAnalysisFinding> findings, int score = 90, string projectName = "HistoryProject")
    {
        var scan = new ProjectScanResult { ProjectPath = projectPath, ProjectName = projectName };
        scan.ProjectFolderExists = true;
        scan.ProjectJsonExists = true;
        scan.ProjectJsonParsed = true;
        foreach (var workflowPath in findings.Select(finding => finding.WorkflowPath).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            scan.Workflows.Add(new UiPathWorkflowInfo
            {
                Name = Path.GetFileName(workflowPath),
                RelativePath = workflowPath!,
                FullPath = Path.Combine(projectPath, workflowPath!),
                Analysis = new UiPathWorkflowAnalysis
                {
                    FileName = Path.GetFileName(workflowPath),
                    RelativePath = workflowPath!,
                    Complexity = new UiPathWorkflowComplexity
                    {
                        WorkflowPath = workflowPath!,
                        ComplexityScore = 10,
                        ComplexityLevel = UiPathWorkflowComplexityLevel.Low,
                        ExecutableActivities = 5
                    }
                }
            });
        }

        var analysis = new UiPathStaticAnalysisResult();
        analysis.Findings.AddRange(findings);

        return new UiPathProjectAnalysisResult
        {
            ProjectScan = scan,
            Analysis = analysis,
            QualityScore = new UiPathQualityScore { Score = score, Grade = score >= 90 ? "A" : "B", ProfileId = "default", ProfileName = "Default" },
            Profile = new UiPathRuleProfile { Id = "default", Name = "Default" }
        };
    }

    private static UiPathAnalysisFinding Finding(
        string ruleId,
        string workflowPath,
        string? activityId = null,
        string activityName = "Assign",
        string displayName = "Assign",
        string message = "Message")
    {
        return new UiPathAnalysisFinding
        {
            RuleId = ruleId,
            RuleName = ruleId,
            Severity = RuleSeverity.Warning,
            Category = RuleCategory.Maintainability,
            Message = message,
            WorkflowPath = workflowPath,
            ActivityId = activityId,
            ActivityName = activityName,
            ActivityDisplayName = displayName,
            PropertyName = "DisplayName",
            CurrentValue = displayName
        };
    }

    private sealed class TempHistoryDirectory : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"rpada-history-{Guid.NewGuid():N}");

        public TempHistoryDirectory(int maxSnapshots = 20)
        {
            MaxSnapshots = maxSnapshots;
            ProjectPath = Path.Combine(root, "project");
            HistoryRoot = Path.Combine(root, "history");
            Directory.CreateDirectory(ProjectPath);
            File.WriteAllText(Path.Combine(ProjectPath, "project.json"), "{\"name\":\"HistoryProject\"}");
        }

        public string ProjectPath { get; }

        public string RootPath => root;

        public string HistoryRoot { get; }

        private int MaxSnapshots { get; }

        public UiPathAnalysisHistoryService CreateService()
        {
            return new UiPathAnalysisHistoryService(new UiPathAnalysisHistoryOptions { StorageRoot = HistoryRoot, MaxSnapshotsPerProject = MaxSnapshots });
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

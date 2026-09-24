using System.Diagnostics;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Git;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Infrastructure.Git;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathGitComparisonServiceTests
{
    [Fact]
    public async Task CompareAsync_AnalyzesTwoCommitsWithoutChangingWorkingTree()
    {
        using var repository = GitProject.Create();
        repository.Write("project.json", "{\"name\":\"GitProject\"}");
        repository.Write("Main.xaml", "<Activity />");
        repository.Commit("baseline");
        var baseline = repository.Head();
        repository.Write("Issue.xaml", "<Activity />");
        repository.Commit("introduce issue");
        var target = repository.Head();
        var service = new UiPathGitComparisonService(new GitCommandRunner(), new FixtureAnalyzer());

        var result = await service.CompareAsync(new UiPathGitComparisonRequest
        {
            ProjectPath = repository.Root,
            BaselineRef = baseline,
            TargetRef = target,
            ProfileId = "default"
        }, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(baseline, result.BaselineCommit);
        Assert.Equal(target, result.TargetCommit);
        Assert.Equal(-10, result.ScoreDelta);
        Assert.Equal(1, result.FindingDelta);
        Assert.Single(result.NewFindings);
        Assert.Empty(result.ResolvedFindings);
        Assert.Contains(result.ChangedFiles, file => file.Contains("Issue.xaml", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(repository.Root, "Issue.xaml")));
        Assert.Equal(target, repository.Head());
    }

    [Fact]
    public async Task CompareAsync_SupportsBranchNamesAndDetectsResolvedFinding()
    {
        using var repository = GitProject.Create();
        repository.Write("project.json", "{\"name\":\"GitProject\"}");
        repository.Write("Main.xaml", "<Activity />");
        repository.Write("Issue.xaml", "<Activity />");
        repository.Commit("baseline issue");
        repository.Run("branch", "baseline-branch");
        File.Delete(Path.Combine(repository.Root, "Issue.xaml"));
        repository.Run("add", "-A");
        repository.Commit("resolve issue");
        repository.Run("branch", "target-branch");
        var service = new UiPathGitComparisonService(new GitCommandRunner(), new FixtureAnalyzer());

        var result = await service.CompareAsync(new UiPathGitComparisonRequest
        {
            ProjectPath = repository.Root,
            BaselineRef = "baseline-branch",
            TargetRef = "target-branch"
        }, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Empty(result.NewFindings);
        Assert.Single(result.ResolvedFindings);
        Assert.Equal(10, result.ScoreDelta);
    }

    [Theory]
    [InlineData("--exec=touch-owned")]
    [InlineData("ref with spaces")]
    public async Task CompareAsync_RejectsUnsafeRefs(string gitRef)
    {
        using var repository = GitProject.Create();
        repository.Write("project.json", "{}");
        repository.Commit("initial");
        var service = new UiPathGitComparisonService(new GitCommandRunner(), new FixtureAnalyzer());

        var result = await service.CompareAsync(new UiPathGitComparisonRequest
        {
            ProjectPath = repository.Root,
            BaselineRef = gitRef,
            TargetRef = "HEAD"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("invalid_ref", result.ErrorCode);
    }

    private sealed class FixtureAnalyzer : IUiPathProjectAnalyzer
    {
        public UiPathProjectAnalysisResult Analyze(string projectPath, string? profileId = null)
        {
            var hasIssue = File.Exists(Path.Combine(projectPath, "Issue.xaml"));
            var analysis = new UiPathStaticAnalysisResult();
            if (hasIssue)
            {
                analysis.Findings.Add(new UiPathAnalysisFinding
                {
                    RuleId = "RPA007",
                    RuleName = "Generic Activity Display Name",
                    Severity = RuleSeverity.Suggestion,
                    Category = RuleCategory.Naming,
                    Message = "Generic display name.",
                    WorkflowPath = "Issue.xaml",
                    ActivityId = "Click_1",
                    PropertyName = "DisplayName"
                });
            }
            return new UiPathProjectAnalysisResult
            {
                ProjectScan = new ProjectScanResult { ProjectPath = projectPath, ProjectName = "GitProject" },
                Analysis = analysis,
                QualityScore = new UiPathQualityScore
                {
                    Score = hasIssue ? 90 : 100,
                    Grade = hasIssue ? "A" : "A",
                    ProfileId = "default",
                    ProfileName = "Default",
                    TotalFindings = analysis.TotalFindings
                },
                Profile = new UiPathRuleProfile { Id = "default", Name = "Default" }
            };
        }
    }

    private sealed class GitProject : IDisposable
    {
        private GitProject(string root) => Root = root;

        public string Root { get; }

        public static GitProject Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantGitTest-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var project = new GitProject(root);
            project.Run("init", "--initial-branch=main");
            project.Run("config", "user.email", "test@rpadevassistant.local");
            project.Run("config", "user.name", "RPA Dev Assistant Test");
            return project;
        }

        public void Write(string relativePath, string content) => File.WriteAllText(Path.Combine(Root, relativePath), content);

        public void Commit(string message)
        {
            Run("add", "-A");
            Run("commit", "-m", message);
        }

        public string Head() => Run("rev-parse", "HEAD").Trim();

        public string Run(params string[] arguments)
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException(error);
            return output;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}

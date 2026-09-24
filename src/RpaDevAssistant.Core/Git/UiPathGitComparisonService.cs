using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.Git;

public sealed class UiPathGitComparisonService : IUiPathGitComparisonService
{
    private readonly IGitCommandRunner git;
    private readonly IUiPathProjectAnalyzer analyzer;

    public UiPathGitComparisonService(IGitCommandRunner git, IUiPathProjectAnalyzer analyzer)
    {
        this.git = git;
        this.analyzer = analyzer;
    }

    public async Task<UiPathGitComparisonResult> CompareAsync(UiPathGitComparisonRequest request, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(request.ProjectPath) || !File.Exists(Path.Combine(request.ProjectPath, "project.json")))
        {
            return Failed("ProjectPath must point to a UiPath project.", "invalid_project");
        }
        if (!IsSafeRef(request.BaselineRef) || !IsSafeRef(request.TargetRef))
        {
            return Failed("Git references contain unsupported characters.", "invalid_ref");
        }

        var projectPath = Path.GetFullPath(request.ProjectPath);
        var rootResult = await git.RunAsync(projectPath, ["rev-parse", "--show-toplevel"], cancellationToken).ConfigureAwait(false);
        if (!rootResult.Success || string.IsNullOrWhiteSpace(rootResult.StandardOutput))
        {
            return Failed("The selected UiPath project is not inside a Git repository.", "not_git_repository");
        }
        var repositoryRoot = Path.GetFullPath(rootResult.StandardOutput.Trim());
        var prefixResult = await git.RunAsync(projectPath, ["rev-parse", "--show-prefix"], cancellationToken).ConfigureAwait(false);
        if (!prefixResult.Success)
        {
            return Failed("The UiPath project is outside the resolved Git repository.", "project_outside_repository");
        }
        var projectRelative = prefixResult.StandardOutput.Trim().Replace('\\', '/').TrimEnd('/');
        if (projectRelative.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
        {
            return Failed("The UiPath project is outside the resolved Git repository.", "project_outside_repository");
        }

        var baselineCommit = await ResolveCommit(repositoryRoot, request.BaselineRef, cancellationToken).ConfigureAwait(false);
        var targetCommit = await ResolveCommit(repositoryRoot, request.TargetRef, cancellationToken).ConfigureAwait(false);
        if (baselineCommit is null || targetCommit is null)
        {
            return Failed("One or both Git references could not be resolved to commits.", "unknown_ref");
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantGit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            var baselineProject = await ExportProject(repositoryRoot, projectRelative, baselineCommit, Path.Combine(tempRoot, "baseline"), cancellationToken).ConfigureAwait(false);
            var targetProject = await ExportProject(repositoryRoot, projectRelative, targetCommit, Path.Combine(tempRoot, "target"), cancellationToken).ConfigureAwait(false);
            if (baselineProject is null || targetProject is null)
            {
                return Failed("The UiPath project does not exist at one or both selected Git references.", "project_missing_at_ref");
            }

            var baseline = await analyzer.AnalyzeAsync(baselineProject, request.ProfileId, cancellationToken).ConfigureAwait(false);
            var target = await analyzer.AnalyzeAsync(targetProject, request.ProfileId, cancellationToken).ConfigureAwait(false);
            var diffArguments = new List<string> { "diff", "--name-status", $"{baselineCommit}..{targetCommit}" };
            if (!string.IsNullOrWhiteSpace(projectRelative))
            {
                diffArguments.Add("--");
                diffArguments.Add(projectRelative);
            }
            var changedFilesResult = await git.RunAsync(repositoryRoot, diffArguments, cancellationToken).ConfigureAwait(false);
            var changedFiles = changedFilesResult.Success
                ? changedFilesResult.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [];
            var comparison = CompareFindings(baseline.Analysis.Findings, target.Analysis.Findings);
            return new UiPathGitComparisonResult
            {
                Success = true,
                Message = "Git references analyzed and compared successfully.",
                RepositoryRoot = repositoryRoot,
                ProjectRelativePath = projectRelative,
                BaselineRef = request.BaselineRef,
                BaselineCommit = baselineCommit,
                TargetRef = request.TargetRef,
                TargetCommit = targetCommit,
                BaselineScore = baseline.QualityScore.Score,
                TargetScore = target.QualityScore.Score,
                BaselineFindings = baseline.Analysis.TotalFindings,
                TargetFindings = target.Analysis.TotalFindings,
                ChangedFiles = changedFiles,
                NewFindings = comparison.New,
                ResolvedFindings = comparison.Resolved,
                ChangedFindings = comparison.Changed
            };
        }
        catch (InvalidDataException)
        {
            return Failed("A Git archive could not be extracted safely.", "invalid_archive");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private async Task<string?> ResolveCommit(string repositoryRoot, string reference, CancellationToken cancellationToken)
    {
        var result = await git.RunAsync(repositoryRoot, ["rev-parse", "--verify", "--end-of-options", $"{reference}^{{commit}}"], cancellationToken).ConfigureAwait(false);
        var commit = result.StandardOutput.Trim();
        return result.Success && commit.Length == 40 && commit.All(Uri.IsHexDigit) ? commit.ToLowerInvariant() : null;
    }

    private async Task<string?> ExportProject(string repositoryRoot, string projectRelative, string commit, string outputRoot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputRoot);
        var archivePath = Path.Combine(outputRoot, "project.tar");
        var arguments = new List<string> { "archive", "--format=tar", $"--output={archivePath}", commit };
        if (!string.IsNullOrWhiteSpace(projectRelative) && projectRelative != ".")
        {
            arguments.Add("--");
            arguments.Add(projectRelative);
        }
        var result = await git.RunAsync(repositoryRoot, arguments, cancellationToken).ConfigureAwait(false);
        if (!result.Success || !File.Exists(archivePath))
        {
            return null;
        }
        var extracted = Path.Combine(outputRoot, "files");
        Directory.CreateDirectory(extracted);
        TarFile.ExtractToDirectory(archivePath, extracted, overwriteFiles: false);
        var project = string.IsNullOrWhiteSpace(projectRelative) || projectRelative == "."
            ? extracted
            : Path.Combine(extracted, projectRelative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(Path.Combine(project, "project.json")) ? project : null;
    }

    private static FindingComparison CompareFindings(IReadOnlyList<UiPathAnalysisFinding> baseline, IReadOnlyList<UiPathAnalysisFinding> target)
    {
        var before = baseline.GroupBy(Identity).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var after = target.GroupBy(Identity).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var added = after.Where(pair => !before.ContainsKey(pair.Key)).Select(pair => pair.Value).ToArray();
        var resolved = before.Where(pair => !after.ContainsKey(pair.Key)).Select(pair => pair.Value).ToArray();
        var changed = after
            .Where(pair => before.TryGetValue(pair.Key, out var previous) && !Content(previous).Equals(Content(pair.Value), StringComparison.Ordinal))
            .Select(pair => new UiPathGitChangedFinding { Before = before[pair.Key], After = pair.Value })
            .ToArray();
        return new FindingComparison(added, resolved, changed);
    }

    private static string Identity(UiPathAnalysisFinding finding) => string.Join('|',
        finding.RuleId,
        NormalizePath(finding.WorkflowPath),
        finding.ActivityId ?? finding.ActivityName ?? string.Empty,
        finding.PropertyName ?? string.Empty);

    private static string Content(UiPathAnalysisFinding finding)
    {
        var value = string.Join('|', finding.Severity, finding.Category, finding.Message, finding.Recommendation, finding.OccurrenceCount);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string NormalizePath(string? value) => (value ?? string.Empty).Replace('\\', '/').TrimStart('/');

    private static bool IsSafeRef(string value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 200
        && value[0] != '-'
        && value.All(character => char.IsLetterOrDigit(character) || "._/@{}^~:+-".Contains(character, StringComparison.Ordinal));

    private static UiPathGitComparisonResult Failed(string message, string code) => new() { Success = false, Message = message, ErrorCode = code };

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch (IOException) { }
    }

    private sealed record FindingComparison(
        IReadOnlyList<UiPathAnalysisFinding> New,
        IReadOnlyList<UiPathAnalysisFinding> Resolved,
        IReadOnlyList<UiPathGitChangedFinding> Changed);
}

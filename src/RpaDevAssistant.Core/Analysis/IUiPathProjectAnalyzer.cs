namespace RpaDevAssistant.Core.Analysis;

using RpaDevAssistant.Core.Analysis.Profiles;

public interface IUiPathProjectAnalyzer
{
    UiPathProjectAnalysisResult Analyze(string projectPath, string? profileId = null);

    Task<UiPathProjectAnalysisResult> AnalyzeAsync(
        string projectPath,
        string? profileId = null,
        CancellationToken cancellationToken = default) => Task.FromResult(Analyze(projectPath, profileId));

    Task<UiPathProjectAnalysisResult> AnalyzeAsync(
        string projectPath,
        UiPathRuleProfile profile,
        CancellationToken cancellationToken = default) => AnalyzeAsync(projectPath, profile.Id, cancellationToken);
}

namespace RpaDevAssistant.Core.Analysis;

public interface IUiPathProjectAnalyzer
{
    UiPathProjectAnalysisResult Analyze(string projectPath, string? profileId = null);

    Task<UiPathProjectAnalysisResult> AnalyzeAsync(
        string projectPath,
        string? profileId = null,
        CancellationToken cancellationToken = default) => Task.FromResult(Analyze(projectPath, profileId));
}

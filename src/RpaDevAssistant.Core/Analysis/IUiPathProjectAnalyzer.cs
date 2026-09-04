namespace RpaDevAssistant.Core.Analysis;

public interface IUiPathProjectAnalyzer
{
    UiPathProjectAnalysisResult Analyze(string projectPath, string? profileId = null);
}

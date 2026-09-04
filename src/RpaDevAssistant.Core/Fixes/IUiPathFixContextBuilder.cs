using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.Fixes;

public interface IUiPathFixContextBuilder
{
    UiPathFixContext Build(UiPathProjectAnalysisResult analysis, UiPathAnalysisFinding finding, string? locale = null);
}

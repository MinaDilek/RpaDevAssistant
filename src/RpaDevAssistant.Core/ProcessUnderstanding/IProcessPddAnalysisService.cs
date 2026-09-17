using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.ProcessUnderstanding;

public interface IProcessPddAnalysisService
{
    ProcessPddAnalysisResult Analyze(UiPathProjectAnalysisResult analysis, ProcessPddDocument pdd, string? locale = null);
}

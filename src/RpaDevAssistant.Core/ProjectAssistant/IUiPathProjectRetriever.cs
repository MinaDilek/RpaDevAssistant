using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.ProjectAssistant;

public interface IUiPathProjectRetriever
{
    IReadOnlyList<UiPathProjectEvidence> Retrieve(UiPathProjectAnalysisResult analysis, UiPathProjectQuestion request);
}

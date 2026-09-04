using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.ProjectAssistant;

public interface IUiPathProjectAssistantPromptBuilder
{
    UiPathProjectAssistantPrompt Build(UiPathProjectQuestion request, UiPathProjectAnalysisResult analysis, IReadOnlyList<UiPathProjectEvidence> evidence);
}

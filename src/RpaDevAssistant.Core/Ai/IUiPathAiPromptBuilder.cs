namespace RpaDevAssistant.Core.Ai;

public interface IUiPathAiPromptBuilder
{
    UiPathAiPrompt Build(UiPathAiReviewRequest request);
}

namespace RpaDevAssistant.Core.Fixes;

public interface IUiPathAiFixPromptBuilder
{
    UiPathAiFixPrompt Build(UiPathFixContext context);
}

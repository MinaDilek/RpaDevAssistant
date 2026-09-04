namespace RpaDevAssistant.Core.ProjectAssistant;

public interface IUiPathProjectAssistantAiProvider
{
    string ProviderName { get; }

    bool IsConfigured { get; }

    Task<UiPathProjectAnswer> AnswerAsync(UiPathProjectAssistantPrompt prompt, CancellationToken cancellationToken);
}

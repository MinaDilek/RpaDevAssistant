namespace RpaDevAssistant.Core.Fixes;

public interface IUiPathAiFixAdvisor
{
    string ProviderName { get; }

    bool IsConfigured { get; }

    Task<UiPathFixSuggestion> SuggestAsync(UiPathAiFixPrompt prompt, CancellationToken cancellationToken);
}

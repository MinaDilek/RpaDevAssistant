namespace RpaDevAssistant.Core.Ai;

public interface IUiPathAiReviewProvider
{
    string ProviderName { get; }

    bool IsConfigured { get; }

    Task<UiPathAiReviewResult> ReviewAsync(UiPathAiPrompt prompt, CancellationToken cancellationToken);
}

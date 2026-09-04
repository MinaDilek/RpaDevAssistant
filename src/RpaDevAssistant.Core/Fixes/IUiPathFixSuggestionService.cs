namespace RpaDevAssistant.Core.Fixes;

public interface IUiPathFixSuggestionService
{
    Task<UiPathFixSuggestionResult> SuggestAsync(UiPathFixSuggestionRequest request, CancellationToken cancellationToken);

    Task<UiPathFixSuggestionsBulkResult> SuggestAllDeterministicAsync(UiPathFixSuggestionsBulkRequest request, CancellationToken cancellationToken);
}

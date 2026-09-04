namespace RpaDevAssistant.Core.Fixes;

public interface IUiPathFixSuggestionValidator
{
    UiPathFixSuggestionValidationResult Validate(UiPathFixContext context, UiPathFixSuggestion suggestion);
}

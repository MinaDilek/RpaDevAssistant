namespace RpaDevAssistant.Core.Fixes;

public interface IUiPathFixSuggestionProvider
{
    IReadOnlyCollection<string> SupportedRuleIds { get; }

    bool RequiresAi { get; }

    UiPathFixSuggestion? Suggest(UiPathFixContext context);
}

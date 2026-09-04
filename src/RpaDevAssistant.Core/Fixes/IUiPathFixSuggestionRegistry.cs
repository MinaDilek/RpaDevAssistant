namespace RpaDevAssistant.Core.Fixes;

public interface IUiPathFixSuggestionRegistry
{
    IUiPathFixSuggestionProvider? FindProvider(string ruleId);

    bool HasProvider(string ruleId);
}

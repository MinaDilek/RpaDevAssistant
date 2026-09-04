namespace RpaDevAssistant.Core.Analysis.RuleCatalog;

public interface IUiPathRuleCatalog
{
    IReadOnlyList<UiPathRuleDefinition> GetAllRules();

    UiPathRuleDefinition? GetRuleById(string id);

    IReadOnlyList<UiPathRuleDefinition> GetRulesByCategory(RuleCategory category);

    IReadOnlyList<UiPathRuleDefinition> GetRulesByScope(UiPathRuleScope scope);
}

namespace RpaDevAssistant.Core.Analysis.CustomRules;

public interface IUiPathCustomRuleRepository
{
    IReadOnlyList<UiPathCustomRuleDefinition> GetRules();

    UiPathCustomRuleDefinition? GetRule(string id);

    UiPathCustomRuleDefinition SaveRule(UiPathCustomRuleDefinition rule);

    UiPathCustomRuleImportResult ImportRules(IEnumerable<UiPathCustomRuleDefinition> rules, bool overwrite = false);

    UiPathCustomRuleStore ExportRules();
}

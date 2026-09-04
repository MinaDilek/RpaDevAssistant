namespace RpaDevAssistant.Core.Analysis.CustomRules;

public interface IUiPathCustomRuleValidator
{
    UiPathCustomRuleValidationResult Validate(UiPathCustomRuleDefinition rule, IEnumerable<string>? existingRuleIds = null);
}

namespace RpaDevAssistant.Core.Fixes.Apply;

public interface IUiPathMutationPolicy
{
    UiPathFixApplyValidationResult Validate(UiPathFixSuggestion suggestion, UiPathFixApplyRequest request);

    bool CanAutoApplyRule(string ruleId);
}

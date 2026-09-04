namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed class UiPathMutationPolicy : IUiPathMutationPolicy
{
    private static readonly HashSet<string> AutoApplyRuleIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "RPA007"
    };

    private static readonly HashSet<string> AllowedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "DisplayName"
    };

    public UiPathFixApplyValidationResult Validate(UiPathFixSuggestion suggestion, UiPathFixApplyRequest request)
    {
        var result = new UiPathFixApplyValidationResult();

        if (!AutoApplyRuleIds.Contains(request.RuleId) || !AutoApplyRuleIds.Contains(suggestion.RuleId))
        {
            result.Errors.Add("Only RPA007 DisplayName fixes can be applied automatically.");
        }

        if (!AllowedProperties.Contains(request.PropertyName) || !AllowedProperties.Contains(suggestion.PropertyName ?? string.Empty))
        {
            result.Errors.Add("Only DisplayName property changes are allowed.");
        }

        if (suggestion.RiskLevel != UiPathFixRiskLevel.Low)
        {
            result.Errors.Add("Only low-risk fix suggestions can be applied automatically.");
        }

        if (suggestion.RequiresAi)
        {
            result.Errors.Add("AI-assisted fix suggestions cannot be applied automatically.");
        }

        if (!suggestion.CanAutoApply)
        {
            result.Errors.Add("This fix suggestion is marked as manual-only.");
        }

        if (suggestion.FixType is not UiPathFixSuggestionType.NamingChange and not UiPathFixSuggestionType.PropertyChange)
        {
            result.Errors.Add("Only property naming fixes can be applied automatically.");
        }

        return result;
    }

    public bool CanAutoApplyRule(string ruleId)
    {
        return AutoApplyRuleIds.Contains(ruleId);
    }
}

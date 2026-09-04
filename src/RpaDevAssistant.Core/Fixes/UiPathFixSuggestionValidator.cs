namespace RpaDevAssistant.Core.Fixes;

public sealed class UiPathFixSuggestionValidator : IUiPathFixSuggestionValidator
{
    public UiPathFixSuggestionValidationResult Validate(UiPathFixContext context, UiPathFixSuggestion suggestion)
    {
        var result = new UiPathFixSuggestionValidationResult();

        if (!suggestion.RuleId.Equals(context.Finding.RuleId, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add("Fix suggestion rule id does not match the finding rule id.");
        }

        if (!string.IsNullOrWhiteSpace(suggestion.WorkflowPath) && context.Workflow is null)
        {
            result.Errors.Add("Workflow path no longer exists in the current project analysis.");
        }

        if (!string.IsNullOrWhiteSpace(suggestion.ActivityId) && context.Activity is null)
        {
            result.Errors.Add("Activity id no longer exists in the current workflow analysis.");
        }

        if (suggestion.FixType is UiPathFixSuggestionType.PropertyChange or UiPathFixSuggestionType.NamingChange)
        {
            if (string.IsNullOrWhiteSpace(suggestion.SuggestedValue))
            {
                result.Errors.Add("Suggested property value cannot be empty.");
            }

            if (!string.IsNullOrWhiteSpace(suggestion.PropertyName) && context.Activity is not null)
            {
                var currentValue = suggestion.PropertyName.Equals("DisplayName", StringComparison.OrdinalIgnoreCase)
                    ? context.Activity.DisplayName
                    : context.Activity.Properties.TryGetValue(suggestion.PropertyName, out var propertyValue)
                        ? propertyValue
                        : null;

                if (!string.Equals(currentValue, suggestion.CurrentValue, StringComparison.Ordinal))
                {
                    result.Errors.Add("Current property value has changed since the suggestion was generated.");
                }
            }
        }

        if (suggestion.CanAutoApply)
        {
            result.Warnings.Add("Auto-apply is not enabled for this version.");
        }

        return result;
    }
}

using System.Globalization;

namespace RpaDevAssistant.Core.Analysis.CustomRules;

public sealed class UiPathCustomRuleValidator : IUiPathCustomRuleValidator
{
    private static readonly HashSet<string> SupportedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Activity.Name",
        "Activity.DisplayName",
        "Activity.Property",
        "Workflow.Name",
        "Workflow.Path",
        "Workflow.ActivityCount",
        "Workflow.ExecutableActivityCount",
        "Workflow.MaxNestingDepth",
        "Project.Compatibility",
        "Project.IsReFramework",
        "Dependency.Name",
        "Dependency.Version"
    };

    private static readonly HashSet<string> NumericFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Workflow.ActivityCount",
        "Workflow.ExecutableActivityCount",
        "Workflow.MaxNestingDepth"
    };

    private static readonly HashSet<string> BooleanFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Project.IsReFramework"
    };

    public UiPathCustomRuleValidationResult Validate(UiPathCustomRuleDefinition rule, IEnumerable<string>? existingRuleIds = null)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(rule.Id))
        {
            errors.Add("Rule ID is required.");
        }
        else if (rule.Id.StartsWith("RPA", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Custom rule ID cannot use the built-in RPA prefix.");
        }
        else if (existingRuleIds?.Any(id => id.Equals(rule.Id, StringComparison.OrdinalIgnoreCase)) == true)
        {
            errors.Add($"Rule ID '{rule.Id}' already exists.");
        }

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            errors.Add("Rule name is required.");
        }

        if (rule.Conditions.Count == 0)
        {
            errors.Add("At least one condition is required.");
        }

        if (rule.Weight < 0)
        {
            errors.Add("Weight must be greater than or equal to 0.");
        }

        if (rule.MaxPenalty < 0)
        {
            errors.Add("MaxPenalty must be greater than or equal to 0.");
        }

        foreach (var condition in rule.Conditions)
        {
            ValidateCondition(condition, errors);
        }

        return new UiPathCustomRuleValidationResult { Errors = errors };
    }

    private static void ValidateCondition(UiPathRuleCondition condition, List<string> errors)
    {
        if (!SupportedFields.Contains(condition.Field))
        {
            errors.Add($"Field '{condition.Field}' is not supported.");
            return;
        }

        if (NumericFields.Contains(condition.Field))
        {
            if (!IsNumericOperator(condition.Operator))
            {
                errors.Add($"Operator '{condition.Operator}' is not valid for numeric field '{condition.Field}'.");
            }

            if (condition.Operator is not UiPathRuleConditionOperator.Exists and not UiPathRuleConditionOperator.NotExists
                && !double.TryParse(condition.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                errors.Add($"Value for numeric field '{condition.Field}' must be a number.");
            }

            return;
        }

        if (BooleanFields.Contains(condition.Field))
        {
            if (condition.Operator is not UiPathRuleConditionOperator.Equals and not UiPathRuleConditionOperator.NotEquals
                and not UiPathRuleConditionOperator.Exists and not UiPathRuleConditionOperator.NotExists)
            {
                errors.Add($"Operator '{condition.Operator}' is not valid for boolean field '{condition.Field}'.");
            }

            if (condition.Operator is UiPathRuleConditionOperator.Equals or UiPathRuleConditionOperator.NotEquals
                && !bool.TryParse(condition.Value, out _))
            {
                errors.Add($"Value for boolean field '{condition.Field}' must be true or false.");
            }

            return;
        }

        if (condition.Field.Equals("Activity.Property", StringComparison.OrdinalIgnoreCase))
        {
            ValidateActivityPropertyCondition(condition, errors);
            return;
        }

        if (condition.Operator is UiPathRuleConditionOperator.GreaterThan
            or UiPathRuleConditionOperator.GreaterThanOrEqual
            or UiPathRuleConditionOperator.LessThan
            or UiPathRuleConditionOperator.LessThanOrEqual)
        {
            errors.Add($"Operator '{condition.Operator}' is not valid for string field '{condition.Field}'.");
        }
    }

    private static void ValidateActivityPropertyCondition(UiPathRuleCondition condition, List<string> errors)
    {
        var propertyName = ResolvePropertyName(condition);
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            errors.Add("Activity.Property conditions require propertyName or a value such as 'TimeoutMS=120000'.");
        }

        if (condition.Operator is UiPathRuleConditionOperator.GreaterThan
            or UiPathRuleConditionOperator.GreaterThanOrEqual
            or UiPathRuleConditionOperator.LessThan
            or UiPathRuleConditionOperator.LessThanOrEqual)
        {
            var expected = ResolveExpectedValue(condition);
            if (!double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                errors.Add($"Value for numeric Activity.Property condition '{propertyName}' must be a number.");
            }
        }
    }

    private static string? ResolvePropertyName(UiPathRuleCondition condition)
    {
        if (!string.IsNullOrWhiteSpace(condition.PropertyName))
        {
            return condition.PropertyName;
        }

        var value = condition.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var separatorIndex = value.IndexOf('=');
        if (separatorIndex < 0)
        {
            separatorIndex = value.IndexOf(':');
        }

        return separatorIndex > 0 ? value[..separatorIndex].Trim() : value.Trim();
    }

    private static string? ResolveExpectedValue(UiPathRuleCondition condition)
    {
        if (!string.IsNullOrWhiteSpace(condition.CompareValue))
        {
            return condition.CompareValue;
        }

        if (!string.IsNullOrWhiteSpace(condition.PropertyName))
        {
            return condition.Value;
        }

        var value = condition.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var separatorIndex = value.IndexOf('=');
        if (separatorIndex < 0)
        {
            separatorIndex = value.IndexOf(':');
        }

        return separatorIndex > 0 ? value[(separatorIndex + 1)..].Trim() : value;
    }

    private static bool IsNumericOperator(UiPathRuleConditionOperator @operator)
    {
        return @operator is UiPathRuleConditionOperator.Equals
            or UiPathRuleConditionOperator.NotEquals
            or UiPathRuleConditionOperator.GreaterThan
            or UiPathRuleConditionOperator.GreaterThanOrEqual
            or UiPathRuleConditionOperator.LessThan
            or UiPathRuleConditionOperator.LessThanOrEqual
            or UiPathRuleConditionOperator.Exists
            or UiPathRuleConditionOperator.NotExists;
    }
}

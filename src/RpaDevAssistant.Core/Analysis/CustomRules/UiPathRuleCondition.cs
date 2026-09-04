namespace RpaDevAssistant.Core.Analysis.CustomRules;

public sealed record UiPathRuleCondition
{
    public required string Field { get; init; }

    public required UiPathRuleConditionOperator Operator { get; init; }

    public string? PropertyName { get; init; }

    public string? Value { get; init; }

    public string? CompareValue { get; init; }

    public bool CaseSensitive { get; init; }
}

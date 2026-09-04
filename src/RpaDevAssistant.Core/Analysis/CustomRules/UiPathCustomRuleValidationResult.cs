namespace RpaDevAssistant.Core.Analysis.CustomRules;

public sealed record UiPathCustomRuleValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<string> Errors { get; init; } = [];
}

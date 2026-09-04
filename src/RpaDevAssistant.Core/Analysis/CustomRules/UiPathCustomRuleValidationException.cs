namespace RpaDevAssistant.Core.Analysis.CustomRules;

public sealed class UiPathCustomRuleValidationException : Exception
{
    public UiPathCustomRuleValidationException(IReadOnlyList<string> errors)
        : base(string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}

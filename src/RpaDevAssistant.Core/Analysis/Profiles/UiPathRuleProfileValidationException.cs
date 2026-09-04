namespace RpaDevAssistant.Core.Analysis.Profiles;

public sealed class UiPathRuleProfileValidationException : Exception
{
    public UiPathRuleProfileValidationException(IReadOnlyList<string> errors)
        : base(string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}

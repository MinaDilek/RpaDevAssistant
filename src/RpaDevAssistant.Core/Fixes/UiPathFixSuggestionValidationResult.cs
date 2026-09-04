namespace RpaDevAssistant.Core.Fixes;

public sealed record UiPathFixSuggestionValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<string> Errors { get; } = [];

    public List<string> Warnings { get; } = [];
}

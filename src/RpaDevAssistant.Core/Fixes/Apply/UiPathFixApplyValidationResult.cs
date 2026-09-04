namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathFixApplyValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<string> Errors { get; init; } = [];

    public List<string> Warnings { get; init; } = [];
}

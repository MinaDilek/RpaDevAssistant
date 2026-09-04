namespace RpaDevAssistant.Core.Fixes;

public sealed record UiPathFixSuggestionResult
{
    public bool IsAvailable => Suggestion is not null;

    public UiPathFixSuggestion? Suggestion { get; init; }

    public string? Message { get; init; }

    public UiPathFixSuggestionValidationResult? Validation { get; init; }
}

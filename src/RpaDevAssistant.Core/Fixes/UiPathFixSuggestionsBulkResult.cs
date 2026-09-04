namespace RpaDevAssistant.Core.Fixes;

public sealed record UiPathFixSuggestionsBulkResult
{
    public int TotalFindings { get; init; }

    public int FixableFindings { get; init; }

    public IReadOnlyList<UiPathFixSuggestion> Suggestions { get; init; } = [];
}

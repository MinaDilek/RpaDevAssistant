namespace RpaDevAssistant.Core.Fixes;

public sealed record UiPathFixSuggestionsBulkRequest
{
    public required string ProjectPath { get; init; }

    public string? ProfileId { get; init; }

    public int? MaxSuggestions { get; init; }

    public string? Locale { get; init; }
}

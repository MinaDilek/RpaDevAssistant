namespace RpaDevAssistant.Api.Requests;

public sealed record FixSuggestionsAllUiPathProjectRequest
{
    public string? ProjectPath { get; init; }

    public string? ProfileId { get; init; }

    public int? MaxSuggestions { get; init; }

    public string? Locale { get; init; }
}

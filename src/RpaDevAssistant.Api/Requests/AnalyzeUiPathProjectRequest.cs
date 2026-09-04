namespace RpaDevAssistant.Api.Requests;

public sealed record AnalyzeUiPathProjectRequest
{
    public string? ProjectPath { get; init; }

    public string? ProfileId { get; init; }

    public string? Locale { get; init; }
}

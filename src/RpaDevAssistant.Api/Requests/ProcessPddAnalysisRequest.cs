namespace RpaDevAssistant.Api.Requests;

public sealed record ProcessPddAnalysisRequest
{
    public string ProjectPath { get; init; } = string.Empty;
    public string? PddPath { get; init; }
    public string? PddFileName { get; init; }
    public string? PddContent { get; init; }
    public string? Locale { get; init; }
}

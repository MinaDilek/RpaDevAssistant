namespace RpaDevAssistant.Api.Requests;

public sealed record ScanUiPathProjectRequest
{
    public string? ProjectPath { get; init; }
}

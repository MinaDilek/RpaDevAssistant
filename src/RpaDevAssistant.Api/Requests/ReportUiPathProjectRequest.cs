namespace RpaDevAssistant.Api.Requests;

public sealed record ReportUiPathProjectRequest
{
    public string? ProjectPath { get; init; }

    public string? ProfileId { get; init; }

    public string? Format { get; init; }

    public string? Locale { get; init; }
}

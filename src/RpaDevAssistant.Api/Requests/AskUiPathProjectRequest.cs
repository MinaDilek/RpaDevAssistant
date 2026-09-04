namespace RpaDevAssistant.Api.Requests;

public sealed record AskUiPathProjectRequest
{
    public string? ProjectPath { get; init; }

    public string? ProfileId { get; init; }

    public string? PreferredWorkflowPath { get; init; }

    public string? Question { get; init; }

    public int? MaxEvidenceItems { get; init; }

    public string? Locale { get; init; }
}

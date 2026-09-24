namespace RpaDevAssistant.Api.Requests;

public sealed record ApplyAllFixesUiPathProjectRequest
{
    public string? ProjectPath { get; init; }

    public string? ProfileId { get; init; }

    public string? Locale { get; init; }

    public int? MaxFixes { get; init; }

    public bool CreateBackup { get; init; } = true;
}

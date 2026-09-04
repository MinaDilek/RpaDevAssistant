namespace RpaDevAssistant.Core.ProjectAssistant;

public sealed record UiPathProjectQuestion
{
    public required string ProjectPath { get; init; }

    public required string Question { get; init; }

    public string? ProfileId { get; init; }

    public string? PreferredWorkflowPath { get; init; }

    public int? MaxEvidenceItems { get; init; }

    public string? Locale { get; init; }
}

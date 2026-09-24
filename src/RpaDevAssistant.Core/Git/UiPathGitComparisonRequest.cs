namespace RpaDevAssistant.Core.Git;

public sealed record UiPathGitComparisonRequest
{
    public required string ProjectPath { get; init; }

    public required string BaselineRef { get; init; }

    public required string TargetRef { get; init; }

    public string? ProfileId { get; init; }
}

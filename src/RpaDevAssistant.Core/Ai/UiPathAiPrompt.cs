namespace RpaDevAssistant.Core.Ai;

public sealed record UiPathAiPrompt
{
    public required string SystemInstructions { get; init; }

    public required string UserContext { get; init; }

    public required UiPathAiReviewScope Scope { get; init; }

    public string? WorkflowPath { get; init; }

    public string? Locale { get; init; }
}

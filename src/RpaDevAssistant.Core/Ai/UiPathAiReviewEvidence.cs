namespace RpaDevAssistant.Core.Ai;

public sealed record UiPathAiReviewEvidence
{
    public required string Statement { get; init; }

    public string? RuleId { get; init; }

    public string? WorkflowPath { get; init; }

    public string? ActivityName { get; init; }
}

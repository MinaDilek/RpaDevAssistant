namespace RpaDevAssistant.Core.Ai;

public sealed record UiPathAiReviewIssue
{
    public required string Title { get; init; }

    public UiPathAiIssueSeverity Severity { get; init; }

    public required string Description { get; init; }

    public required string Evidence { get; init; }

    public IReadOnlyList<UiPathAiReviewEvidence> EvidenceItems { get; init; } = [];

    public string? Interpretation { get; init; }

    public required string Recommendation { get; init; }

    public string? WorkflowPath { get; init; }

    public IReadOnlyList<string> RelatedRuleIds { get; init; } = [];
}

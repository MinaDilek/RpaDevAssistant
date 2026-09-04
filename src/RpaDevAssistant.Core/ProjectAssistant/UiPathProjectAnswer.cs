namespace RpaDevAssistant.Core.ProjectAssistant;

public sealed record UiPathProjectAnswer
{
    public required string Answer { get; init; }

    public UiPathProjectAnswerConfidence Confidence { get; init; }

    public UiPathProjectAnswerType AnswerType { get; init; }

    public IReadOnlyList<UiPathProjectEvidence> Evidence { get; init; } = [];

    public IReadOnlyList<string> RelatedWorkflows { get; init; } = [];

    public IReadOnlyList<string> RelatedActivities { get; init; } = [];

    public IReadOnlyList<string> RelatedRuleIds { get; init; } = [];

    public bool UsedAi { get; init; }

    public string? Model { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string? ReasoningSummary { get; init; }

    public string? ErrorMessage { get; init; }
}

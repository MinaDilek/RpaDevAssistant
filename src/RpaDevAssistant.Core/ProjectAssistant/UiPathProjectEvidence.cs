namespace RpaDevAssistant.Core.ProjectAssistant;

public sealed record UiPathProjectEvidence
{
    public UiPathProjectEvidenceType Type { get; init; }

    public string? WorkflowPath { get; init; }

    public string? ActivityName { get; init; }

    public string? ActivityDisplayName { get; init; }

    public string? RuleId { get; init; }

    public string? PropertyName { get; init; }

    public string? Value { get; init; }

    public string? Description { get; init; }

    public double RelevanceScore { get; init; }
}

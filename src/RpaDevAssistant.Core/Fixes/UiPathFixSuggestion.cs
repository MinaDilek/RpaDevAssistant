namespace RpaDevAssistant.Core.Fixes;

public sealed record UiPathFixSuggestion
{
    public string Id { get; init; } = string.Empty;

    public string FixId => Id;

    public string RuleId { get; init; } = string.Empty;

    public required string Title { get; init; }

    public required string Description { get; init; }

    public UiPathFixSuggestionType FixType { get; init; }

    public UiPathFixability Fixability { get; init; } = UiPathFixability.NotFixable;

    public UiPathFixConfidence Confidence { get; init; }

    public UiPathFixRiskLevel RiskLevel { get; init; }

    public string? WorkflowPath { get; init; }

    public string? ActivityId { get; init; }

    public string? ActivityName { get; init; }

    public string? ActivityDisplayName { get; init; }

    public string? PropertyName { get; init; }

    public string? CurrentValue { get; init; }

    public string? CurrentState => CurrentValue ?? BeforePreview;

    public string? SuggestedValue { get; init; }

    public string? ProposedState => SuggestedValue ?? AfterPreview;

    public string? BeforePreview { get; init; }

    public string? AfterPreview { get; init; }

    public UiPathPatchPreview? PatchPreview { get; init; }

    public required string Explanation { get; init; }

    public IReadOnlyList<string> ValidationNotes { get; init; } = [];

    public IReadOnlyList<string> Steps { get; init; } = [];

    public IReadOnlyList<string> Risks { get; init; } = [];

    public bool RequiresUserInput { get; init; }

    public IReadOnlyList<string> UserInputHints { get; init; } = [];

    public bool RequiresAi { get; init; }

    public bool CanAutoApply { get; init; }

    public string? ExpectedFileHash { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string? ErrorMessage { get; init; }
}

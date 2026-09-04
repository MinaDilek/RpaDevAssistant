namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathFixApplyRequest
{
    public required string ProjectPath { get; init; }

    public required string FixSuggestionId { get; init; }

    public required string RuleId { get; init; }

    public required string WorkflowPath { get; init; }

    public string? ActivityId { get; init; }

    public required string PropertyName { get; init; }

    public string? ExpectedCurrentValue { get; init; }

    public required string SuggestedValue { get; init; }

    public string? ExpectedFileHash { get; init; }

    public bool CreateBackup { get; init; } = true;
}

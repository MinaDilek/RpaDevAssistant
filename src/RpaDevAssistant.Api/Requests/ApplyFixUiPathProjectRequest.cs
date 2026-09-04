namespace RpaDevAssistant.Api.Requests;

public sealed record ApplyFixUiPathProjectRequest
{
    public string? ProjectPath { get; init; }

    public string? FixSuggestionId { get; init; }

    public string? RuleId { get; init; }

    public string? WorkflowPath { get; init; }

    public string? ActivityId { get; init; }

    public string? PropertyName { get; init; }

    public string? ExpectedCurrentValue { get; init; }

    public string? SuggestedValue { get; init; }

    public string? ExpectedFileHash { get; init; }

    public bool CreateBackup { get; init; } = true;
}

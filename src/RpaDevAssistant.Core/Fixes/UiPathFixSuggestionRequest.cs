namespace RpaDevAssistant.Core.Fixes;

public sealed record UiPathFixSuggestionRequest
{
    public required string ProjectPath { get; init; }

    public string? ProfileId { get; init; }

    public required string RuleId { get; init; }

    public string? WorkflowPath { get; init; }

    public string? ActivityId { get; init; }

    public string? PropertyName { get; init; }

    public bool UseAi { get; init; }

    public string? Locale { get; init; }
}

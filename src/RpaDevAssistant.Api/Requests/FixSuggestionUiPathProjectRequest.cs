namespace RpaDevAssistant.Api.Requests;

public sealed record FixSuggestionUiPathProjectRequest
{
    public string? ProjectPath { get; init; }

    public string? ProfileId { get; init; }

    public string? RuleId { get; init; }

    public string? WorkflowPath { get; init; }

    public string? ActivityId { get; init; }

    public string? PropertyName { get; init; }

    public bool UseAi { get; init; }

    public string? Locale { get; init; }
}

namespace RpaDevAssistant.Core.Analysis;

public sealed record UiPathAffectedActivity
{
    public string? ActivityId { get; init; }

    public string? StableId { get; init; }

    public string? ActivityPath { get; init; }

    public required string ActivityName { get; init; }

    public required string ActivityDisplayName { get; init; }

    public string? PropertyName { get; init; }

    public string? CurrentValue { get; init; }
}

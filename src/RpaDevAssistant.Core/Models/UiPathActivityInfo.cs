namespace RpaDevAssistant.Core.Models;

public sealed record UiPathActivityInfo
{
    public required string ActivityId { get; init; }

    public string? ParentActivityId { get; init; }

    public string? StableId { get; init; }

    public string? ActivityPath { get; init; }

    public required string Name { get; init; }

    public required string DisplayName { get; init; }

    public required string TypeName { get; init; }

    public string? Namespace { get; init; }

    public int Depth { get; init; }

    public required string XamlFile { get; init; }

    public IReadOnlyDictionary<string, string?> Arguments { get; init; } = new Dictionary<string, string?>();

    public IReadOnlyDictionary<string, string?> Properties { get; init; } = new Dictionary<string, string?>();
}

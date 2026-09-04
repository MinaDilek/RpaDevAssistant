namespace RpaDevAssistant.Core.Analysis.Profiles;

public sealed record UiPathRuleProfile
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<UiPathRuleConfiguration> Rules { get; init; } = [];
}

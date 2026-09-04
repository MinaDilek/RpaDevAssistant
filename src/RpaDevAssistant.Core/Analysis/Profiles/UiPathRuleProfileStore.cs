namespace RpaDevAssistant.Core.Analysis.Profiles;

public sealed record UiPathRuleProfileStore
{
    public int SchemaVersion { get; init; } = 1;

    public IReadOnlyList<UiPathRuleProfile> Profiles { get; init; } = [];
}

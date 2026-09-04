namespace RpaDevAssistant.Core.Analysis.CustomRules;

public sealed record UiPathCustomRuleStore
{
    public int SchemaVersion { get; init; } = 1;

    public IReadOnlyList<UiPathCustomRuleDefinition> Rules { get; init; } = [];
}

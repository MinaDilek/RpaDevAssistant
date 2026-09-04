using RpaDevAssistant.Core.Analysis.CustomRules;

namespace RpaDevAssistant.Api.Requests;

public sealed record ImportCustomRulesRequest
{
    public IReadOnlyList<UiPathCustomRuleDefinition> Rules { get; init; } = [];

    public bool Overwrite { get; init; }
}

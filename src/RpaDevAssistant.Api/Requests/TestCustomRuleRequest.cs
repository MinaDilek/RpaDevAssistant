using RpaDevAssistant.Core.Analysis.CustomRules;

namespace RpaDevAssistant.Api.Requests;

public sealed record TestCustomRuleRequest
{
    public required string ProjectPath { get; init; }

    public required UiPathCustomRuleDefinition Rule { get; init; }
}

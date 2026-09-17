namespace RpaDevAssistant.Core.Models;

public sealed record UiPathVariableInfo
{
    public required string Name { get; init; }

    public string? Type { get; init; }

    public string? DefaultValue { get; init; }

    public string? Scope { get; init; }

    public string? ScopeActivityId { get; init; }
}

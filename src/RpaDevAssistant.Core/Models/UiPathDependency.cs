namespace RpaDevAssistant.Core.Models;

public sealed record UiPathDependency
{
    public required string Name { get; init; }

    public string? Version { get; init; }
}

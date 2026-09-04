namespace RpaDevAssistant.Core.Models;

public sealed record UiPathProjectInfo
{
    public string? Name { get; init; }

    public string? Compatibility { get; init; }

    public IReadOnlyList<UiPathDependency> Dependencies { get; init; } = [];
}

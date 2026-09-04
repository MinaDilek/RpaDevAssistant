namespace RpaDevAssistant.Core.Models;

public sealed record UiPathArgumentInfo
{
    public required string Name { get; init; }

    public string? Direction { get; init; }

    public string? Type { get; init; }
}

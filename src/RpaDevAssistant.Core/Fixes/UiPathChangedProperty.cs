namespace RpaDevAssistant.Core.Fixes;

public sealed record UiPathChangedProperty
{
    public required string Name { get; init; }

    public string? Before { get; init; }

    public string? After { get; init; }
}

namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathActivityLocator
{
    public required string WorkflowPath { get; init; }

    public string? XamlElementName { get; init; }

    public string? DisplayName { get; init; }

    public string? IdRef { get; init; }

    public string? ActivityPath { get; init; }
}

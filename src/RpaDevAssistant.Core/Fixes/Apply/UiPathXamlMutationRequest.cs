namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathXamlMutationRequest
{
    public required string ProjectPath { get; init; }

    public required string WorkflowPath { get; init; }

    public required string WorkflowFullPath { get; init; }

    public required UiPathActivityLocator Locator { get; init; }

    public required string PropertyName { get; init; }

    public string? ExpectedCurrentValue { get; init; }

    public required string SuggestedValue { get; init; }
}

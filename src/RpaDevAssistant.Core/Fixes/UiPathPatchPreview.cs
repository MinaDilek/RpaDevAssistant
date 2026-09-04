namespace RpaDevAssistant.Core.Fixes;

public sealed record UiPathPatchPreview
{
    public UiPathPatchPreviewFormat Format { get; init; }

    public string? WorkflowPath { get; init; }

    public string? Description { get; init; }

    public string? Before { get; init; }

    public string? After { get; init; }

    public IReadOnlyList<UiPathChangedProperty> ChangedProperties { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}

namespace RpaDevAssistant.Core.Models;

public sealed record UiPathFolderInfo
{
    public required string RelativePath { get; init; }

    public int WorkflowCount { get; init; }
}

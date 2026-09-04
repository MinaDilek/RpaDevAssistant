namespace RpaDevAssistant.Core.Models;

public sealed record UiPathWorkflowInfo
{
    public required string Name { get; init; }

    public required string RelativePath { get; init; }

    public required string FullPath { get; init; }

    public UiPathWorkflowAnalysis? Analysis { get; init; }

    public int ActivityCount => Analysis?.ActivityCount ?? 0;
}

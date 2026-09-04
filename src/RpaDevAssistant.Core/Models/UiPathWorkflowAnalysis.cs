namespace RpaDevAssistant.Core.Models;

public sealed class UiPathWorkflowAnalysis
{
    public required string FileName { get; init; }

    public required string RelativePath { get; init; }

    public int ActivityCount => Activities.Count;

    public List<UiPathActivityInfo> Activities { get; } = [];

    public List<UiPathArgumentInfo> Arguments { get; } = [];

    public List<string> ParseErrors { get; } = [];

    public List<string> ParseWarnings { get; } = [];

    public RpaDevAssistant.Core.Analysis.UiPathWorkflowComplexity? Complexity { get; set; }
}

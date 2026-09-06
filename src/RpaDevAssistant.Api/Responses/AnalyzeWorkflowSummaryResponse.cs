using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Flowcharts;

namespace RpaDevAssistant.Api.Responses;

public sealed record AnalyzeWorkflowSummaryResponse
{
    public required string RelativePath { get; init; }

    public int ActivityCount { get; init; }

    public IReadOnlyList<UiPathActivityInfo> Activities { get; init; } = [];

    public IReadOnlyList<UiPathArgumentInfo> Arguments { get; init; } = [];

    public IReadOnlyList<string> ParseErrors { get; init; } = [];

    public UiPathWorkflowComplexity? Complexity { get; init; }

    public UiPathWorkflowStructureType StructureType { get; init; } = UiPathWorkflowStructureType.Unknown;

    public bool ContainsFlowchart { get; init; }

    public int FlowchartCount { get; init; }

    public static AnalyzeWorkflowSummaryResponse From(UiPathWorkflowInfo workflow)
    {
        return new AnalyzeWorkflowSummaryResponse
        {
            RelativePath = workflow.RelativePath,
            ActivityCount = workflow.ActivityCount,
            Activities = workflow.Analysis?.Activities ?? [],
            Arguments = workflow.Analysis?.Arguments ?? [],
            ParseErrors = workflow.Analysis?.ParseErrors ?? [],
            Complexity = workflow.Analysis?.Complexity,
            StructureType = workflow.Analysis?.StructureType ?? UiPathWorkflowStructureType.Unknown,
            ContainsFlowchart = workflow.Analysis?.ContainsFlowchart ?? false,
            FlowchartCount = workflow.Analysis?.FlowchartCount ?? 0
        };
    }
}

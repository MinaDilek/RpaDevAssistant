using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Api.Responses;

public sealed record AnalyzeWorkflowSummaryResponse
{
    public required string RelativePath { get; init; }

    public int ActivityCount { get; init; }

    public IReadOnlyList<UiPathActivityInfo> Activities { get; init; } = [];

    public IReadOnlyList<UiPathArgumentInfo> Arguments { get; init; } = [];

    public IReadOnlyList<string> ParseErrors { get; init; } = [];

    public UiPathWorkflowComplexity? Complexity { get; init; }

    public static AnalyzeWorkflowSummaryResponse From(UiPathWorkflowInfo workflow)
    {
        return new AnalyzeWorkflowSummaryResponse
        {
            RelativePath = workflow.RelativePath,
            ActivityCount = workflow.ActivityCount,
            Activities = workflow.Analysis?.Activities ?? [],
            Arguments = workflow.Analysis?.Arguments ?? [],
            ParseErrors = workflow.Analysis?.ParseErrors ?? [],
            Complexity = workflow.Analysis?.Complexity
        };
    }
}

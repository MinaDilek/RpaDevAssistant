using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Ai;

public sealed record UiPathAiReviewRequest
{
    public string? ProjectName { get; init; }

    public string? Compatibility { get; init; }

    public bool IsReFramework { get; init; }

    public int WorkflowCount { get; init; }

    public int TotalActivityCount { get; init; }

    public string? SelectedWorkflow { get; init; }

    public IReadOnlyList<UiPathAiWorkflowContext> Workflows { get; init; } = [];

    public IReadOnlyList<UiPathAnalysisFinding> DeterministicFindings { get; init; } = [];

    public IReadOnlyList<UiPathDependency> Dependencies { get; init; } = [];

    public UiPathAiReviewScope ReviewScope { get; init; }

    public string? AdditionalInstructions { get; init; }

    public string? Locale { get; init; }
}

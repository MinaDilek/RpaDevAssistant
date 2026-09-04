using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.ProjectAssistant;

namespace RpaDevAssistant.Core.Fixes;

public sealed record UiPathFixContext
{
    public required ProjectScanResult Project { get; init; }

    public UiPathWorkflowInfo? Workflow { get; init; }

    public UiPathActivityInfo? Activity { get; init; }

    public UiPathActivityInfo? ParentActivity { get; init; }

    public IReadOnlyList<UiPathActivityInfo> NearbyActivities { get; init; } = [];

    public required UiPathAnalysisFinding Finding { get; init; }

    public IReadOnlyList<UiPathAnalysisFinding> RelatedFindings { get; init; } = [];

    public required UiPathRuleProfile RuleProfile { get; init; }

    public required WorkflowInvocationGraph InvocationGraph { get; init; }

    public string? Locale { get; init; }
}

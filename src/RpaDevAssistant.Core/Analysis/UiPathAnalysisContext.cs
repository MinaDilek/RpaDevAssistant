using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis;

public sealed record UiPathAnalysisContext
{
    public required ProjectScanResult Project { get; init; }

    public string ProjectPath => Project.ProjectPath;

    public IEnumerable<UiPathWorkflowInfo> Workflows => Project.Workflows;

    public IEnumerable<UiPathWorkflowAnalysis> WorkflowAnalyses => Project.Workflows
        .Select(workflow => workflow.Analysis)
        .Where(analysis => analysis is not null)
        .Cast<UiPathWorkflowAnalysis>();
}

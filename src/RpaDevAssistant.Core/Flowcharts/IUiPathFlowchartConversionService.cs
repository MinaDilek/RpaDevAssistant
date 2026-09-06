using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Flowcharts;

public interface IUiPathFlowchartConversionService
{
    Task<UiPathFlowchartConversionResult> AnalyzeAsync(string projectPath, string workflowPath, CancellationToken cancellationToken = default);
}

public sealed record UiPathFlowchartConversionResult
{
    public required string WorkflowPath { get; init; }

    public UiPathWorkflowStructureType StructureType { get; init; }

    public UiPathFlowchartGraph? Graph { get; init; }

    public UiPathFlowchartConversionAssessment? Assessment { get; init; }

    public UiPathFlowchartConversionPlan? Plan { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public string? WorkflowHash { get; init; }

    public bool WritesFiles => false;
}

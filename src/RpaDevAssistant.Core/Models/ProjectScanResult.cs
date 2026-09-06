namespace RpaDevAssistant.Core.Models;

using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Dependencies;
using RpaDevAssistant.Core.Flowcharts;

public sealed class ProjectScanResult
{
    public bool IsValid => Errors.Count == 0 && ProjectJsonExists && ProjectJsonParsed;

    public string? ProjectName { get; set; }

    public required string ProjectPath { get; init; }

    public string? Compatibility { get; set; }

    public bool IsReFramework { get; set; }

    public int WorkflowCount => Workflows.Count;

    public int TotalActivityCount => Workflows.Sum(workflow => workflow.ActivityCount);

    public IReadOnlyDictionary<string, int> ComplexityDistribution => Workflows
        .Select(workflow => workflow.Analysis?.Complexity)
        .Where(complexity => complexity is not null)
        .GroupBy(complexity => complexity!.ComplexityLevel.ToString(), StringComparer.OrdinalIgnoreCase)
        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

    public UiPathWorkflowComplexitySummary ComplexitySummary
    {
        get
        {
            var complexities = Workflows
                .Select(workflow => workflow.Analysis?.Complexity)
                .Where(complexity => complexity is not null)
                .Cast<UiPathWorkflowComplexity>()
                .ToArray();

            return new UiPathWorkflowComplexitySummary
            {
                TotalWorkflowCount = Workflows.Count,
                LowCount = complexities.Count(complexity => complexity.ComplexityLevel == UiPathWorkflowComplexityLevel.Low),
                MediumCount = complexities.Count(complexity => complexity.ComplexityLevel == UiPathWorkflowComplexityLevel.Medium),
                HighCount = complexities.Count(complexity => complexity.ComplexityLevel == UiPathWorkflowComplexityLevel.High),
                VeryHighCount = complexities.Count(complexity => complexity.ComplexityLevel == UiPathWorkflowComplexityLevel.VeryHigh),
                TopComplexWorkflows = complexities
                    .OrderByDescending(complexity => complexity.ComplexityScore)
                    .ThenBy(complexity => complexity.WorkflowPath, StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .Select(complexity => new UiPathTopComplexWorkflow
                    {
                        WorkflowPath = complexity.WorkflowPath,
                        ComplexityScore = complexity.ComplexityScore,
                        ComplexityLevel = complexity.ComplexityLevel,
                        ExecutableActivityCount = complexity.ExecutableActivityCount,
                        MaxNestingDepth = complexity.MaxNestingDepth
                    })
                    .ToArray()
            };
        }
    }

    public IReadOnlyList<RpaDevAssistant.Core.Analysis.UiPathWorkflowComplexity> TopComplexWorkflows => Workflows
        .Select(workflow => workflow.Analysis?.Complexity)
        .Where(complexity => complexity is not null)
        .OrderByDescending(complexity => complexity!.ComplexityScore)
        .ThenBy(complexity => complexity!.WorkflowPath, StringComparer.OrdinalIgnoreCase)
        .Take(10)
        .ToArray()!;

    public IReadOnlyDictionary<string, int> ActivityTypeCounts => Workflows
        .SelectMany(workflow => workflow.Analysis?.Activities ?? [])
        .GroupBy(activity => activity.Name, StringComparer.OrdinalIgnoreCase)
        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

    public bool ProjectFolderExists { get; set; }

    public bool ProjectJsonExists { get; set; }

    public bool ProjectJsonParsed { get; set; }

    public List<UiPathWorkflowInfo> Workflows { get; } = [];

    public List<UiPathDependency> Dependencies { get; } = [];

    public UiPathDependencySummary? DependencyAnalysis { get; set; }

    public UiPathFlowchartAnalysisSummary? FlowchartAnalysis { get; set; }

    public List<UiPathFolderInfo> Folders { get; } = [];

    public List<string> Errors { get; } = [];

    public List<string> Warnings { get; } = [];
}

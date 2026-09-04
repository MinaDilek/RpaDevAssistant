using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.ProjectAssistant;

public sealed class UiPathWorkflowGraphBuilder : IUiPathWorkflowGraphBuilder
{
    public WorkflowInvocationGraph Build(ProjectScanResult project)
    {
        var workflowPaths = project.Workflows
            .Select(workflow => NormalizePath(workflow.RelativePath))
            .ToDictionary(path => path, path => path, StringComparer.OrdinalIgnoreCase);

        var edges = new List<WorkflowInvocationEdge>();
        foreach (var workflow in project.Workflows)
        {
            foreach (var activity in workflow.Analysis?.Activities ?? [])
            {
                if (!UiPathActivityAliasNormalizer.NormalizeActivityName(activity.Name).Equals("InvokeWorkflowFile", StringComparison.OrdinalIgnoreCase)
                    || !activity.Properties.TryGetValue("WorkflowFileName", out var rawReference)
                    || string.IsNullOrWhiteSpace(rawReference))
                {
                    continue;
                }

                var isDynamic = IsDynamicReference(rawReference);
                var normalizedReference = NormalizePath(rawReference);
                workflowPaths.TryGetValue(normalizedReference, out var callee);
                edges.Add(new WorkflowInvocationEdge
                {
                    CallerWorkflowPath = NormalizePath(workflow.RelativePath),
                    CalleeWorkflowPath = isDynamic ? null : callee,
                    RawReference = rawReference,
                    IsDynamicReference = isDynamic
                });
            }
        }

        return new WorkflowInvocationGraph
        {
            Workflows = workflowPaths.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            Edges = edges
        };
    }

    public static string NormalizePath(string path)
    {
        return path.Trim()
            .Trim('"')
            .Trim('\'')
            .Replace('\\', '/')
            .TrimStart('/');
    }

    private static bool IsDynamicReference(string value)
    {
        var trimmedValue = value.Trim();
        return trimmedValue.StartsWith("[", StringComparison.Ordinal)
            || trimmedValue.Contains('&', StringComparison.Ordinal)
            || trimmedValue.Contains("+", StringComparison.Ordinal)
            || trimmedValue.Contains("(", StringComparison.Ordinal)
            || trimmedValue.Contains(")", StringComparison.Ordinal);
    }
}

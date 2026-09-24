using System.Text;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis.Rules;

public sealed class CircularWorkflowReferenceRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA036";

    public override string Name => "Circular Workflow Reference";

    public override string Description => "Detects circular invocation dependencies between workflows.";

    public override RuleSeverity Severity => RuleSeverity.Critical;

    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        var knownWorkflows = context.Workflows
            .Select(w => NormalizePath(w.RelativePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Map: caller -> list of (callee, activity)
        var invocationMap = new Dictionary<string, List<(string Callee, UiPathActivityInfo Activity)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var workflow in context.WorkflowAnalyses)
        {
            var callerPath = NormalizePath(workflow.RelativePath);
            if (!invocationMap.ContainsKey(callerPath))
            {
                invocationMap[callerPath] = [];
            }

            foreach (var activity in workflow.Activities.Where(a => UiPathActivityClassifier.IsNamed(a, "InvokeWorkflowFile")))
            {
                if (!activity.Properties.TryGetValue("WorkflowFileName", out var referencedPath)
                    || string.IsNullOrWhiteSpace(referencedPath)
                    || IsDynamicExpression(referencedPath))
                {
                    continue;
                }

                var normalizedCallee = NormalizePath(referencedPath);
                if (knownWorkflows.Contains(normalizedCallee))
                {
                    invocationMap[callerPath].Add((normalizedCallee, activity));
                }
            }
        }

        // Cycle detection per starting node using DFS
        var reportedCycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var startWorkflow in invocationMap.Keys)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var recStack = new List<string>();

            foreach (var finding in FindCycles(startWorkflow, startWorkflow, visited, recStack, invocationMap, context, reportedCycles))
            {
                yield return finding;
            }
        }
    }

    private IEnumerable<UiPathAnalysisFinding> FindCycles(
        string root,
        string current,
        HashSet<string> visited,
        List<string> recStack,
        Dictionary<string, List<(string Callee, UiPathActivityInfo Activity)>> invocationMap,
        UiPathAnalysisContext context,
        HashSet<string> reportedCycles)
    {
        visited.Add(current);
        recStack.Add(current);

        if (invocationMap.TryGetValue(current, out var invocations))
        {
            foreach (var (callee, activity) in invocations)
            {
                var cycleIndex = recStack.IndexOf(callee);
                if (cycleIndex >= 0)
                {
                    // Found cycle: slice from cycleIndex to end
                    var cyclePath = recStack.Skip(cycleIndex).Concat([callee]).ToList();
                    var cycleKey = string.Join(" -> ", cyclePath.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));

                    if (reportedCycles.Add(cycleKey))
                    {
                        var cycleDisplay = string.Join(" -> ", cyclePath);
                        var workflowAnalysis = context.WorkflowAnalyses
                            .FirstOrDefault(w => NormalizePath(w.RelativePath).Equals(current, StringComparison.OrdinalIgnoreCase))
                            ?? context.WorkflowAnalyses.First();

                        yield return CreateFinding(
                            $"Circular workflow reference detected: {cycleDisplay}",
                            "Refactor workflows to break circular invocation cycles and avoid infinite recursion or stack overflow.",
                            workflowAnalysis,
                            activity,
                            "WorkflowFileName",
                            cycleDisplay);
                    }
                }
                else if (!visited.Contains(callee))
                {
                    foreach (var f in FindCycles(root, callee, visited, recStack, invocationMap, context, reportedCycles))
                    {
                        yield return f;
                    }
                }
            }
        }

        recStack.RemoveAt(recStack.Count - 1);
    }

    private static bool IsDynamicExpression(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("[", StringComparison.Ordinal)
            || trimmed.Contains('&', StringComparison.Ordinal)
            || trimmed.Contains('+', StringComparison.Ordinal)
            || trimmed.Contains('(', StringComparison.Ordinal)
            || trimmed.Contains(')', StringComparison.Ordinal);
    }

    private static string NormalizePath(string path)
    {
        return path.Trim()
            .Trim('"')
            .Trim('\'')
            .Replace('\\', '/')
            .TrimStart('/')
            .Normalize(NormalizationForm.FormC);
    }
}

using System.Text;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis.Rules;

public sealed class UnusedWorkflowRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA037";

    public override string Name => "Unused Workflow";

    public override string Description => "Detects workflows that are never invoked and are not designated entry points.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Maintainability;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        var allWorkflows = context.WorkflowAnalyses.ToList();
        // If there's only 1 workflow or fewer in the project, it cannot be unused
        if (allWorkflows.Count <= 1)
        {
            yield break;
        }

        var normalizedPathMap = allWorkflows
            .ToDictionary(w => NormalizePath(w.RelativePath), w => w, StringComparer.OrdinalIgnoreCase);

        // Identify entry points (e.g. Main.xaml)
        var entryPoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in normalizedPathMap.Keys)
        {
            if (path.Equals("Main.xaml", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/Main.xaml", StringComparison.OrdinalIgnoreCase) ||
                IsTestWorkflow(path))
            {
                entryPoints.Add(path);
            }
        }

        if (entryPoints.Count == 0)
        {
            // Fallback: the first workflow
            entryPoints.Add(normalizedPathMap.Keys.First());
        }

        // Build adjacency list of invocations
        var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in normalizedPathMap.Keys)
        {
            adjacency[path] = [];
        }

        foreach (var workflow in allWorkflows)
        {
            var caller = NormalizePath(workflow.RelativePath);
            foreach (var activity in workflow.Activities.Where(a => UiPathActivityClassifier.IsNamed(a, "InvokeWorkflowFile")))
            {
                if (activity.Properties.TryGetValue("WorkflowFileName", out var raw) &&
                    !string.IsNullOrWhiteSpace(raw))
                {
                    var callee = NormalizePath(raw);
                    if (normalizedPathMap.ContainsKey(callee))
                    {
                        adjacency[caller].Add(callee);
                    }
                }
            }
        }

        // BFS reachability from entry points
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(entryPoints);
        foreach (var ep in entryPoints)
        {
            reachable.Add(ep);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (adjacency.TryGetValue(current, out var callees))
            {
                foreach (var callee in callees)
                {
                    if (reachable.Add(callee))
                    {
                        queue.Enqueue(callee);
                    }
                }
            }
        }

        // Report unreachable non-test workflows
        foreach (var (normalizedPath, workflow) in normalizedPathMap)
        {
            if (reachable.Contains(normalizedPath) || IsTestWorkflow(normalizedPath))
            {
                continue;
            }

            yield return CreateFinding(
                $"Workflow '{workflow.RelativePath}' is never invoked from project entry points.",
                "Remove unused workflow files or connect them to the process execution chain.",
                workflow);
        }
    }

    private static bool IsTestWorkflow(string path)
    {
        return path.Contains("/Tests/", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/Test/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("Tests/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("Test/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("TestCase", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/TestCase", StringComparison.OrdinalIgnoreCase);
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

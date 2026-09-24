using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.ProjectAssistant;

namespace RpaDevAssistant.Core.Scanning;

public sealed class UiPathReFrameworkAnalyzer
{
    private static readonly string[] CoreWorkflowNames =
    [
        "Main.xaml",
        "InitAllSettings.xaml",
        "GetTransactionData.xaml",
        "Process.xaml",
        "SetTransactionStatus.xaml"
    ];

    private readonly IUiPathWorkflowGraphBuilder graphBuilder;

    public UiPathReFrameworkAnalyzer(IUiPathWorkflowGraphBuilder graphBuilder)
    {
        this.graphBuilder = graphBuilder;
    }

    public UiPathReFrameworkAssessment Analyze(ProjectScanResult project)
    {
        var detectedNames = project.Workflows
            .Select(workflow => workflow.Name)
            .Where(name => CoreWorkflowNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var missingNames = CoreWorkflowNames
            .Except(detectedNames, StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var isCandidate = detectedNames.Length >= 3;
        var rootMain = project.Workflows.FirstOrDefault(workflow =>
            UiPathWorkflowGraphBuilder.NormalizePath(workflow.RelativePath).Equals("Main.xaml", StringComparison.OrdinalIgnoreCase));

        var checklist = new List<UiPathReFrameworkChecklistItem>
        {
            Item("root-main", rootMain is null ? "Fail" : "Pass", rootMain?.RelativePath ?? "Main.xaml"),
            Item("canonical-workflows", missingNames.Length == 0 ? "Pass" : "Fail",
                missingNames.Length == 0 ? detectedNames : missingNames),
            Item(
                "main-state-machine",
                rootMain?.Analysis?.ContainsStateMachine == true ? "Pass" : "Fail",
                rootMain?.RelativePath ?? "Main.xaml")
        };

        var malformedCoreWorkflows = project.Workflows
            .Where(workflow => CoreWorkflowNames.Contains(workflow.Name, StringComparer.OrdinalIgnoreCase))
            .Where(workflow => workflow.Analysis is null || workflow.Analysis.ParseErrors.Count > 0)
            .Select(workflow => workflow.RelativePath)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        checklist.Add(Item(
            "core-workflow-parsing",
            malformedCoreWorkflows.Length == 0 && detectedNames.Length > 0 ? "Pass" : "Fail",
            malformedCoreWorkflows.Length == 0 ? detectedNames : malformedCoreWorkflows));

        AddGraphChecks(project, rootMain, missingNames, checklist);

        var configPaths = FindConfigWorkbooks(project.ProjectPath);
        checklist.Add(Item("config-workbook", configPaths.Length > 0 ? "Pass" : "Fail", configPaths));

        var requiredChecksPass = checklist
            .Where(item => item.Id is "root-main" or "canonical-workflows" or "main-state-machine" or "core-workflow-parsing" or "static-framework-reachability")
            .All(item => item.Status == "Pass");

        return new UiPathReFrameworkAssessment
        {
            IsDetected = isCandidate,
            DetectionStatus = !isCandidate ? "NotDetected" : requiredChecksPass ? "Detected" : "Candidate",
            DetectedCoreWorkflows = detectedNames,
            MissingCoreWorkflows = missingNames,
            ChecklistItems = checklist
        };
    }

    private void AddGraphChecks(
        ProjectScanResult project,
        UiPathWorkflowInfo? rootMain,
        IReadOnlyCollection<string> missingNames,
        ICollection<UiPathReFrameworkChecklistItem> checklist)
    {
        WorkflowInvocationGraph graph;
        try
        {
            graph = graphBuilder.Build(project);
        }
        catch (ArgumentException)
        {
            checklist.Add(Item("static-framework-reachability", "Unknown", "duplicate-workflow-path"));
            checklist.Add(Item("static-invoke-integrity", "Unknown", "duplicate-workflow-path"));
            return;
        }

        var brokenStaticReferences = graph.Edges
            .Where(edge => !edge.IsDynamicReference && edge.CalleeWorkflowPath is null)
            .Select(edge => $"{edge.CallerWorkflowPath} -> {edge.RawReference}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        checklist.Add(Item(
            "static-invoke-integrity",
            brokenStaticReferences.Length == 0 ? "Pass" : "Fail",
            brokenStaticReferences));

        if (rootMain is null || missingNames.Count > 0)
        {
            checklist.Add(Item("static-framework-reachability", "Fail", missingNames.ToArray()));
            return;
        }

        var reachable = FindReachable(graph, rootMain.RelativePath);
        var unreachable = project.Workflows
            .Where(workflow => CoreWorkflowNames.Contains(workflow.Name, StringComparer.OrdinalIgnoreCase))
            .Where(workflow => !workflow.Name.Equals("Main.xaml", StringComparison.OrdinalIgnoreCase))
            .Where(workflow => !reachable.Contains(UiPathWorkflowGraphBuilder.NormalizePath(workflow.RelativePath)))
            .Select(workflow => workflow.RelativePath)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hasDynamicReachableEdge = graph.Edges.Any(edge =>
            reachable.Contains(edge.CallerWorkflowPath) && edge.IsDynamicReference);
        checklist.Add(Item(
            "static-framework-reachability",
            unreachable.Length == 0 ? "Pass" : hasDynamicReachableEdge ? "Unknown" : "Fail",
            unreachable.Length == 0 ? reachable.Order(StringComparer.OrdinalIgnoreCase).ToArray() : unreachable));
    }

    private static HashSet<string> FindReachable(WorkflowInvocationGraph graph, string entryPoint)
    {
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            UiPathWorkflowGraphBuilder.NormalizePath(entryPoint)
        };
        var pending = new Queue<string>(reachable);
        while (pending.TryDequeue(out var caller))
        {
            foreach (var callee in graph.Edges
                         .Where(edge => edge.CallerWorkflowPath.Equals(caller, StringComparison.OrdinalIgnoreCase))
                         .Select(edge => edge.CalleeWorkflowPath)
                         .Where(path => path is not null))
            {
                if (reachable.Add(callee!))
                {
                    pending.Enqueue(callee!);
                }
            }
        }

        return reachable;
    }

    private static string[] FindConfigWorkbooks(string projectPath)
    {
        if (!Directory.Exists(projectPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(projectPath, "Config.xlsx", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.rpadevassistant{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(projectPath, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static UiPathReFrameworkChecklistItem Item(string id, string status, params string[] evidence) =>
        new()
        {
            Id = id,
            Status = status,
            Evidence = evidence.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()
        };

}

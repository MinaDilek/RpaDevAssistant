using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.ProjectAssistant;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathWorkflowGraphBuilderTests
{
    [Fact]
    public void Build_WhenAInvokesB_RecordsIncomingCallerForB()
    {
        var graph = Build(Project(
            Workflow("A.xaml", Invoke("B.xaml")),
            Workflow("B.xaml")));

        Assert.Contains("A.xaml", CallersOf(graph, "B.xaml"));
    }

    [Fact]
    public void Build_WhenTwoWorkflowsInvokeSameCallee_RecordsBothCallers()
    {
        var graph = Build(Project(
            Workflow("A.xaml", Invoke("Shared/B.xaml")),
            Workflow("C.xaml", Invoke("Shared/B.xaml")),
            Workflow("Shared/B.xaml")));

        Assert.Equal(["A.xaml", "C.xaml"], CallersOf(graph, "Shared/B.xaml").Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_WhenWorkflowHasNoIncomingReferences_ReturnsEmptyCallerList()
    {
        var graph = Build(Project(
            Workflow("A.xaml", Invoke("B.xaml")),
            Workflow("B.xaml"),
            Workflow("Unused.xaml")));

        Assert.Empty(CallersOf(graph, "Unused.xaml"));
    }

    [Fact]
    public void Build_NormalizesWindowsAndUnixPathSeparators()
    {
        var graph = Build(Project(
            Workflow("A.xaml", Invoke(@"Framework\B.xaml")),
            Workflow("Framework/B.xaml")));

        Assert.Contains("A.xaml", CallersOf(graph, "Framework/B.xaml"));
    }

    [Fact]
    public void Build_DynamicInvokeReferenceDoesNotCreateFalseStaticCaller()
    {
        var graph = Build(Project(
            Workflow("A.xaml", Invoke("[workflowName]")),
            Workflow("B.xaml")));

        Assert.Empty(CallersOf(graph, "B.xaml"));
        Assert.Contains(graph.Edges, edge => edge.CallerWorkflowPath == "A.xaml" && edge.IsDynamicReference && edge.CalleeWorkflowPath is null);
    }

    private static WorkflowInvocationGraph Build(ProjectScanResult project)
    {
        return new UiPathWorkflowGraphBuilder().Build(project);
    }

    private static IReadOnlyList<string> CallersOf(WorkflowInvocationGraph graph, string calleePath)
    {
        var normalizedCallee = UiPathWorkflowGraphBuilder.NormalizePath(calleePath);
        return graph.Edges
            .Where(edge => edge.CalleeWorkflowPath?.Equals(normalizedCallee, StringComparison.OrdinalIgnoreCase) == true)
            .Select(edge => edge.CallerWorkflowPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ProjectScanResult Project(params UiPathWorkflowInfo[] workflows)
    {
        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/project",
            ProjectFolderExists = true,
            ProjectJsonExists = true,
            ProjectJsonParsed = true,
            ProjectName = "GraphFixture"
        };
        project.Workflows.AddRange(workflows);
        return project;
    }

    private static UiPathWorkflowInfo Workflow(string path, params UiPathActivityInfo[] activities)
    {
        var analysis = new UiPathWorkflowAnalysis
        {
            FileName = Path.GetFileName(path),
            RelativePath = path
        };
        analysis.Activities.AddRange(activities.Select(activity => activity with { XamlFile = path }));

        return new UiPathWorkflowInfo
        {
            Name = Path.GetFileName(path),
            RelativePath = path,
            FullPath = $"/tmp/project/{path}",
            Analysis = analysis
        };
    }

    private static UiPathActivityInfo Invoke(string workflowFileName)
    {
        return new UiPathActivityInfo
        {
            ActivityId = Guid.NewGuid().ToString("N"),
            Name = "InvokeWorkflowFile",
            DisplayName = "Invoke Workflow File",
            TypeName = "InvokeWorkflowFile",
            XamlFile = "placeholder.xaml",
            Properties = new Dictionary<string, string?>
            {
                ["WorkflowFileName"] = workflowFileName
            }
        };
    }
}

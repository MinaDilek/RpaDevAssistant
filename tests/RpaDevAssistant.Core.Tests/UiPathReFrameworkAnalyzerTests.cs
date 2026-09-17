using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.ProjectAssistant;
using RpaDevAssistant.Core.Scanning;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathReFrameworkAnalyzerTests
{
    private readonly UiPathReFrameworkAnalyzer analyzer = new(new UiPathWorkflowGraphBuilder());

    [Fact]
    public void Analyze_DetectsCanonicalStructureWithStateMachineAndReachableCoreWorkflows()
    {
        var main = Workflow("Main.xaml", stateMachine: true,
            Invoke("Framework/InitAllSettings.xaml"),
            Invoke("Framework/GetTransactionData.xaml"),
            Invoke("Framework/Process.xaml"),
            Invoke("Framework/SetTransactionStatus.xaml"));
        var project = Project(
            main,
            Workflow("Framework/InitAllSettings.xaml"),
            Workflow("Framework/GetTransactionData.xaml"),
            Workflow("Framework/Process.xaml"),
            Workflow("Framework/SetTransactionStatus.xaml"));

        var result = analyzer.Analyze(project);

        Assert.True(result.IsDetected);
        Assert.Equal("Detected", result.DetectionStatus);
        Assert.Equal("Pass", Check(result, "canonical-workflows").Status);
        Assert.Equal("Pass", Check(result, "main-state-machine").Status);
        Assert.Equal("Pass", Check(result, "static-framework-reachability").Status);
    }

    [Fact]
    public void Analyze_KeepsFilenameOnlyMatchAsCandidateInsteadOfDetailedDetection()
    {
        var project = Project(
            Workflow("InitAllSettings.xaml"),
            Workflow("GetTransactionData.xaml"),
            Workflow("Process.xaml"));

        var result = analyzer.Analyze(project);

        Assert.True(result.IsDetected);
        Assert.Equal("Candidate", result.DetectionStatus);
        Assert.Equal("Fail", Check(result, "root-main").Status);
    }

    [Fact]
    public void Analyze_ReportsUnknownReachabilityWhenDynamicInvokeMayReachCoreWorkflows()
    {
        var project = Project(
            Workflow("Main.xaml", stateMachine: true, Invoke("[workflowPath]")),
            Workflow("Framework/InitAllSettings.xaml"),
            Workflow("Framework/GetTransactionData.xaml"),
            Workflow("Framework/Process.xaml"),
            Workflow("Framework/SetTransactionStatus.xaml"));

        var result = analyzer.Analyze(project);

        Assert.Equal("Unknown", Check(result, "static-framework-reachability").Status);
        Assert.Equal("Candidate", result.DetectionStatus);
    }

    [Fact]
    public void Analyze_ReportsBrokenStaticInvokeAndMalformedCoreWorkflow()
    {
        var malformed = Workflow("Framework/Process.xaml");
        malformed.Analysis!.ParseErrors.Add("Invalid XML");
        var project = Project(
            Workflow("Main.xaml", stateMachine: true, Invoke("Framework/Missing.xaml")),
            Workflow("Framework/InitAllSettings.xaml"),
            Workflow("Framework/GetTransactionData.xaml"),
            malformed,
            Workflow("Framework/SetTransactionStatus.xaml"));

        var result = analyzer.Analyze(project);

        Assert.Equal("Fail", Check(result, "static-invoke-integrity").Status);
        Assert.Contains(Check(result, "static-invoke-integrity").Evidence, value => value.Contains("Missing.xaml", StringComparison.Ordinal));
        Assert.Equal("Fail", Check(result, "core-workflow-parsing").Status);
        Assert.Equal("Candidate", result.DetectionStatus);
    }

    [Fact]
    public void Analyze_DoesNotCountDuplicateCanonicalNamesAsThreeSignals()
    {
        var project = Project(
            Workflow("One/Main.xaml"),
            Workflow("Two/Main.xaml"),
            Workflow("Three/Main.xaml"));

        var result = analyzer.Analyze(project);

        Assert.False(result.IsDetected);
        Assert.Equal("NotDetected", result.DetectionStatus);
        Assert.Single(result.DetectedCoreWorkflows);
    }

    private static UiPathReFrameworkChecklistItem Check(UiPathReFrameworkAssessment assessment, string id) =>
        assessment.ChecklistItems.Single(item => item.Id == id);

    private static ProjectScanResult Project(params UiPathWorkflowInfo[] workflows)
    {
        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/reframework-project",
            ProjectName = "REFramework Test",
            ProjectFolderExists = true,
            ProjectJsonExists = true,
            ProjectJsonParsed = true
        };
        project.Workflows.AddRange(workflows);
        return project;
    }

    private static UiPathWorkflowInfo Workflow(string path, bool stateMachine = false, params UiPathActivityInfo[] activities)
    {
        var analysis = new UiPathWorkflowAnalysis
        {
            FileName = Path.GetFileName(path),
            RelativePath = path,
            ContainsStateMachine = stateMachine
        };
        analysis.Activities.AddRange(activities.Select(activity => activity with { XamlFile = path }));
        return new UiPathWorkflowInfo
        {
            Name = Path.GetFileName(path),
            RelativePath = path,
            FullPath = $"/tmp/reframework-project/{path}",
            Analysis = analysis
        };
    }

    private static UiPathActivityInfo Invoke(string reference) => new()
    {
        ActivityId = Guid.NewGuid().ToString("N"),
        Name = "InvokeWorkflowFile",
        DisplayName = "Invoke Workflow File",
        TypeName = "InvokeWorkflowFile",
        XamlFile = "Main.xaml",
        Properties = new Dictionary<string, string?> { ["WorkflowFileName"] = reference }
    };
}

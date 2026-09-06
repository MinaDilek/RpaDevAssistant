using System.Text.Json;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Models;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathWorkflowComplexitySummaryTests
{
    [Fact]
    public void ComplexitySummary_CountsWorkflowLevelsAndKeepsTotalWorkflowCount()
    {
        var scan = new ProjectScanResult { ProjectPath = "/tmp/project" };
        scan.Workflows.Add(Workflow("Low.xaml", 12, UiPathWorkflowComplexityLevel.Low));
        scan.Workflows.Add(Workflow("Medium.xaml", 42, UiPathWorkflowComplexityLevel.Medium));
        scan.Workflows.Add(Workflow("High.xaml", 95, UiPathWorkflowComplexityLevel.High));
        scan.Workflows.Add(Workflow("VeryHigh.xaml", 180, UiPathWorkflowComplexityLevel.VeryHigh));

        var summary = scan.ComplexitySummary;

        Assert.Equal(4, summary.TotalWorkflowCount);
        Assert.Equal(1, summary.LowCount);
        Assert.Equal(1, summary.MediumCount);
        Assert.Equal(1, summary.HighCount);
        Assert.Equal(1, summary.VeryHighCount);
    }

    [Fact]
    public void ComplexitySummary_ReturnsTopFiveByScoreThenPath()
    {
        var scan = new ProjectScanResult { ProjectPath = "/tmp/project" };
        scan.Workflows.Add(Workflow("Z.xaml", 120, UiPathWorkflowComplexityLevel.High));
        scan.Workflows.Add(Workflow("B.xaml", 200, UiPathWorkflowComplexityLevel.VeryHigh));
        scan.Workflows.Add(Workflow("A.xaml", 200, UiPathWorkflowComplexityLevel.VeryHigh));
        scan.Workflows.Add(Workflow("C.xaml", 190, UiPathWorkflowComplexityLevel.VeryHigh));
        scan.Workflows.Add(Workflow("D.xaml", 180, UiPathWorkflowComplexityLevel.VeryHigh));
        scan.Workflows.Add(Workflow("E.xaml", 170, UiPathWorkflowComplexityLevel.VeryHigh));
        scan.Workflows.Add(Workflow("F.xaml", 160, UiPathWorkflowComplexityLevel.VeryHigh));

        var top = scan.ComplexitySummary.TopComplexWorkflows;

        Assert.Equal(["A.xaml", "B.xaml", "C.xaml", "D.xaml", "E.xaml"], top.Select(workflow => workflow.WorkflowPath));
    }

    [Fact]
    public void ComplexitySummary_UsesFrontendCompatibleJsonContract()
    {
        var scan = new ProjectScanResult { ProjectPath = "/tmp/project" };
        scan.Workflows.Add(Workflow("Main.xaml", 12, UiPathWorkflowComplexityLevel.Low));

        var json = JsonSerializer.Serialize(scan.ComplexitySummary, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"topComplexWorkflows\"", json);
        Assert.DoesNotContain("\"topWorkflows\"", json);
    }

    [Fact]
    public void ComplexitySummary_HandlesEmptyProject()
    {
        var scan = new ProjectScanResult { ProjectPath = "/tmp/project" };

        var summary = scan.ComplexitySummary;

        Assert.Equal(0, summary.TotalWorkflowCount);
        Assert.Equal(0, summary.LowCount);
        Assert.Empty(summary.TopComplexWorkflows);
    }

    private static UiPathWorkflowInfo Workflow(string path, int score, UiPathWorkflowComplexityLevel level)
    {
        return new UiPathWorkflowInfo
        {
            Name = Path.GetFileName(path),
            RelativePath = path,
            FullPath = $"/tmp/project/{path}",
            Analysis = new UiPathWorkflowAnalysis
            {
                FileName = Path.GetFileName(path),
                RelativePath = path,
                Complexity = new UiPathWorkflowComplexity
                {
                    WorkflowPath = path,
                    ComplexityScore = score,
                    ComplexityLevel = level,
                    ExecutableActivities = score,
                    MaxNestingDepth = score / 10
                }
            }
        };
    }
}

using RpaDevAssistant.Core.Scanning;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathProjectScannerTests
{
    [Fact]
    public void Scan_ReturnsValidResult_ForValidUiPathProject()
    {
        using var project = TestUiPathProject.Create();
        project.WriteProjectJson("""
        {
          "name": "SampleProject",
          "targetFramework": "Windows",
          "dependencies": {
            "UiPath.System.Activities": "[23.10.1]"
          }
        }
        """);
        project.WriteWorkflow("Main.xaml");

        var result = new UiPathProjectScanner().Scan(project.RootPath);

        Assert.True(result.IsValid);
        Assert.Equal("SampleProject", result.ProjectName);
        Assert.Equal("Windows", result.Compatibility);
        Assert.NotNull(result.CompatibilityBehavior);
        Assert.Equal(RpaDevAssistant.Core.Compatibility.UiPathRuntimeCompatibility.Windows, result.CompatibilityBehavior.Runtime);
        Assert.True(result.ProjectFolderExists);
        Assert.True(result.ProjectJsonExists);
        Assert.True(result.ProjectJsonParsed);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Scan_ReturnsInvalidResult_WhenProjectJsonDoesNotExist()
    {
        using var project = TestUiPathProject.Create();
        project.WriteWorkflow("Main.xaml");

        var result = new UiPathProjectScanner().Scan(project.RootPath);

        Assert.False(result.IsValid);
        Assert.False(result.ProjectJsonExists);
        Assert.Contains(result.Errors, error => error.Contains("project.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Scan_FindsXamlFilesRecursively()
    {
        using var project = TestUiPathProject.CreateValid();
        project.WriteWorkflow("Framework/InitAllSettings.xaml");
        project.WriteWorkflow("Business/Process.xaml");

        var result = new UiPathProjectScanner().Scan(project.RootPath);

        Assert.Equal(3, result.WorkflowCount);
        Assert.Contains(result.Workflows, workflow => workflow.RelativePath == "Main.xaml");
        Assert.Contains(result.Workflows, workflow => workflow.RelativePath == "Framework/InitAllSettings.xaml");
        Assert.Contains(result.Workflows, workflow => workflow.RelativePath == "Business/Process.xaml");
    }

    [Fact]
    public void Scan_ParsesDependencies()
    {
        using var project = TestUiPathProject.Create();
        project.WriteProjectJson("""
        {
          "name": "DependencyProject",
          "dependencies": {
            "UiPath.System.Activities": "[23.10.1]",
            "UiPath.Excel.Activities": {
              "version": "[2.23.0]"
            }
          }
        }
        """);

        var result = new UiPathProjectScanner().Scan(project.RootPath);

        Assert.Collection(
            result.Dependencies,
            dependency =>
            {
                Assert.Equal("UiPath.Excel.Activities", dependency.Name);
                Assert.Equal("[2.23.0]", dependency.Version);
            },
            dependency =>
            {
                Assert.Equal("UiPath.System.Activities", dependency.Name);
                Assert.Equal("[23.10.1]", dependency.Version);
            });
    }

    [Fact]
    public void Scan_DetectsReFrameworkLikeProject()
    {
        using var project = TestUiPathProject.CreateValid();
        project.WriteWorkflow("Framework/InitAllSettings.xaml");
        project.WriteWorkflow("Framework/GetTransactionData.xaml");
        project.WriteWorkflow("Framework/SetTransactionStatus.xaml");

        var result = new UiPathProjectScanner().Scan(project.RootPath);

        Assert.True(result.IsReFramework);
    }

    [Fact]
    public void Scan_ReturnsErrorResult_WhenProjectJsonIsMalformed()
    {
        using var project = TestUiPathProject.Create();
        project.WriteProjectJson("{ this is not json");

        var result = new UiPathProjectScanner().Scan(project.RootPath);
        Assert.False(result.IsValid);
        Assert.True(result.ProjectJsonExists);
        Assert.False(result.ProjectJsonParsed);
        Assert.Contains(result.Errors, error => error.Contains("parse", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Scan_ContinuesAnalyzingOtherWorkflows_WhenOneXamlIsMalformed()
    {
        using var project = TestUiPathProject.CreateValid();
        project.WriteWorkflow("Broken.xaml", "<Sequence>");

        var result = new UiPathProjectScanner().Scan(project.RootPath);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.WorkflowCount);
        Assert.Contains(result.Workflows, workflow => workflow.RelativePath == "Main.xaml" && workflow.ActivityCount > 0);
        Assert.Contains(result.Workflows, workflow => workflow.RelativePath == "Broken.xaml" && workflow.Analysis!.ParseErrors.Count > 0);
    }

    [Fact]
    public void Scan_ReturnsActivityTypeCounts()
    {
        using var project = TestUiPathProject.Create();
        project.WriteProjectJson("""
        {
          "name": "ActivityCountsProject",
          "dependencies": {}
        }
        """);
        project.WriteWorkflow("Main.xaml", TestUiPathProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <Assign />
              <ui:Click />
            </Sequence.Activities>
          </Sequence>
        """));
        project.WriteWorkflow("Framework/Process.xaml", TestUiPathProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <Assign />
              <ui:LogMessage />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = new UiPathProjectScanner().Scan(project.RootPath);

        Assert.Equal(6, result.TotalActivityCount);
        Assert.Equal(2, result.ActivityTypeCounts["Sequence"]);
        Assert.Equal(2, result.ActivityTypeCounts["Assign"]);
        Assert.Equal(1, result.ActivityTypeCounts["Click"]);
        Assert.Equal(1, result.ActivityTypeCounts["LogMessage"]);
    }

    [Fact]
    public void Scan_ReturnsTotalActivityCount_ForRecursiveProjectScan()
    {
        using var project = TestUiPathProject.CreateValid();
        project.WriteWorkflow("Business/Process.xaml", TestUiPathProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click />
              <ui:TypeInto />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = new UiPathProjectScanner().Scan(project.RootPath);

        Assert.Equal(4, result.TotalActivityCount);
    }
}

file sealed class TestUiPathProject : IDisposable
{
    public string RootPath { get; } = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantTests-{Guid.NewGuid():N}");

    private TestUiPathProject()
    {
        Directory.CreateDirectory(RootPath);
    }

    public static TestUiPathProject Create()
    {
        return new TestUiPathProject();
    }

    public static TestUiPathProject CreateValid()
    {
        var project = new TestUiPathProject();
        project.WriteProjectJson("""
        {
          "name": "SampleProject",
          "targetFramework": "Windows",
          "dependencies": {}
        }
        """);
        project.WriteWorkflow("Main.xaml");
        return project;
    }

    public void WriteProjectJson(string content)
    {
        File.WriteAllText(Path.Combine(RootPath, "project.json"), content);
    }

    public void WriteWorkflow(string relativePath)
    {
        WriteWorkflow(relativePath, WorkflowXaml("<Sequence />"));
    }

    public void WriteWorkflow(string relativePath, string content)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public static string WorkflowXaml(string body)
    {
        return $$"""
        <Activity
          xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:ui="http://schemas.uipath.com/workflow/activities"
          x:Class="Main">
        {{body}}
        </Activity>
        """;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}

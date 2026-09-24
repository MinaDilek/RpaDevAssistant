using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Fixes.Rename;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Parsing;
using RpaDevAssistant.Core.Scanning;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathWorkflowRenameServiceTests
{
    [Fact]
    public async Task RenameAsync_RenamesWorkflowAndUpdatesRootAndCallerRelativeReferences()
    {
        using var project = RenameProject.Create();
        project.Write("Business/Old.xaml", Workflow("Old"));
        project.Write("Main.xaml", Workflow("Main", "Business/Old.xaml"));
        project.Write("Framework/Caller.xaml", Workflow("Caller", "../Business/Old.xaml"));
        var service = CreateService();

        var result = await service.RenameAsync(new UiPathWorkflowRenameRequest
        {
            ProjectPath = project.Root,
            WorkflowPath = "Business/Old.xaml",
            NewWorkflowPath = "Business/NewName.xaml"
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(File.Exists(Path.Combine(project.Root, "Business", "Old.xaml")));
        Assert.True(File.Exists(Path.Combine(project.Root, "Business", "NewName.xaml")));
        Assert.Contains("Business/NewName.xaml", File.ReadAllText(Path.Combine(project.Root, "Main.xaml")));
        Assert.Contains("../Business/NewName.xaml", File.ReadAllText(Path.Combine(project.Root, "Framework", "Caller.xaml")));
        Assert.Equal(2, result.UpdatedCallerWorkflows.Count);
        Assert.NotNull(result.BackupId);
        Assert.True(File.Exists(Path.Combine(project.Root, ".rpadevassistant", "backups", result.BackupId!, "backup.json")));
    }

    [Fact]
    public async Task RenameAsync_LeavesDynamicReferenceUntouchedAndReportsManualReview()
    {
        using var project = RenameProject.Create();
        project.Write("Old.xaml", Workflow("Old"));
        project.Write("Main.xaml", Workflow("Main", "[workflowPath]"));

        var result = await CreateService().RenameAsync(new UiPathWorkflowRenameRequest
        {
            ProjectPath = project.Root,
            WorkflowPath = "Old.xaml",
            NewWorkflowPath = "New.xaml"
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("[workflowPath]", File.ReadAllText(Path.Combine(project.Root, "Main.xaml")));
        Assert.Contains("Main.xaml", result.DynamicReferencesRequiringReview);
    }

    [Fact]
    public async Task RenameAsync_UpdatesStaticNestedWorkflowFileNameProperty()
    {
        using var project = RenameProject.Create();
        project.Write("Old.xaml", Workflow("Old"));
        project.Write("Main.xaml", """
            <?xml version="1.0" encoding="utf-8"?>
            <Activity x:Class="Main" xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:ui="http://schemas.uipath.com/workflow/activities">
              <Sequence><ui:InvokeWorkflowFile><ui:InvokeWorkflowFile.WorkflowFileName><InArgument x:TypeArguments="x:String"><![CDATA[Old.xaml]]></InArgument></ui:InvokeWorkflowFile.WorkflowFileName></ui:InvokeWorkflowFile></Sequence>
            </Activity>
            """);

        var result = await CreateService().RenameAsync(new UiPathWorkflowRenameRequest
        {
            ProjectPath = project.Root,
            WorkflowPath = "Old.xaml",
            NewWorkflowPath = "New.xaml"
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("<![CDATA[New.xaml]]>", File.ReadAllText(Path.Combine(project.Root, "Main.xaml")));
    }

    [Fact]
    public async Task RenameAsync_RejectsStaleHashWithoutChangingFiles()
    {
        using var project = RenameProject.Create();
        project.Write("Old.xaml", Workflow("Old"));

        var result = await CreateService().RenameAsync(new UiPathWorkflowRenameRequest
        {
            ProjectPath = project.Root,
            WorkflowPath = "Old.xaml",
            NewWorkflowPath = "New.xaml",
            ExpectedFileHash = "stale"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("stale_file_hash", result.ErrorCode);
        Assert.True(File.Exists(Path.Combine(project.Root, "Old.xaml")));
        Assert.False(File.Exists(Path.Combine(project.Root, "New.xaml")));
    }

    [Fact]
    public async Task RenameAsync_RollsBackTargetAndCallersWhenPostValidationFails()
    {
        using var project = RenameProject.Create();
        var originalTarget = Workflow("Old");
        var originalCaller = Workflow("Main", "Old.xaml");
        project.Write("Old.xaml", originalTarget);
        project.Write("Main.xaml", originalCaller);
        var realParser = new UiPathXamlParser();
        var failingParser = new FailingRenamedWorkflowParser(realParser, "New.xaml");
        var service = new UiPathWorkflowRenameService(new UiPathProjectScanner(realParser), failingParser, new UiPathMutationLock());

        var result = await service.RenameAsync(new UiPathWorkflowRenameRequest
        {
            ProjectPath = project.Root,
            WorkflowPath = "Old.xaml",
            NewWorkflowPath = "New.xaml"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.RolledBack);
        Assert.Equal(originalTarget, File.ReadAllText(Path.Combine(project.Root, "Old.xaml")));
        Assert.Equal(originalCaller, File.ReadAllText(Path.Combine(project.Root, "Main.xaml")));
        Assert.False(File.Exists(Path.Combine(project.Root, "New.xaml")));
    }

    [Theory]
    [InlineData("../Outside.xaml")]
    [InlineData("New.txt")]
    public async Task RenameAsync_RejectsUnsafeOrInvalidDestination(string destination)
    {
        using var project = RenameProject.Create();
        project.Write("Old.xaml", Workflow("Old"));

        var result = await CreateService().RenameAsync(new UiPathWorkflowRenameRequest
        {
            ProjectPath = project.Root,
            WorkflowPath = "Old.xaml",
            NewWorkflowPath = destination
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(File.Exists(Path.Combine(project.Root, "Old.xaml")));
    }

    private static UiPathWorkflowRenameService CreateService()
    {
        var parser = new UiPathXamlParser();
        return new UiPathWorkflowRenameService(new UiPathProjectScanner(parser), parser, new UiPathMutationLock());
    }

    private static string Workflow(string name, string? invoke = null) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Activity x:Class="{name}" xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:ui="http://schemas.uipath.com/workflow/activities">
          <Sequence DisplayName="{name}">
            {(invoke is null ? "" : $"<ui:InvokeWorkflowFile DisplayName=\"Invoke\" WorkflowFileName=\"{invoke}\" />")}
          </Sequence>
        </Activity>
        """;

    private sealed class FailingRenamedWorkflowParser(IUiPathXamlParser inner, string renamedFile) : IUiPathXamlParser
    {
        public UiPathWorkflowAnalysis Parse(string xamlPath, string projectRoot)
        {
            var result = inner.Parse(xamlPath, projectRoot);
            if (Path.GetFileName(xamlPath).Equals(renamedFile, StringComparison.OrdinalIgnoreCase))
            {
                result.ParseErrors.Add("Simulated post-validation failure.");
            }
            return result;
        }
    }

    private sealed class RenameProject : IDisposable
    {
        private RenameProject(string root) => Root = root;

        public string Root { get; }

        public static RenameProject Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantRename-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "project.json"), "{\"name\":\"RenameTest\"}");
            return new RenameProject(root);
        }

        public void Write(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}

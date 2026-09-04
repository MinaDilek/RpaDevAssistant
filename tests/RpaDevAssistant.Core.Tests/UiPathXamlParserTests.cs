using RpaDevAssistant.Core.Parsing;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathXamlParserTests
{
    [Fact]
    public void Parse_ReadsSimpleSequence()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", """
        <Sequence xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities" DisplayName="Main Sequence" />
        """);

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        var activity = Assert.Single(analysis.Activities);
        Assert.Equal("Sequence", activity.Name);
        Assert.Equal("Main Sequence", activity.DisplayName);
        Assert.Equal(0, activity.Depth);
        Assert.Empty(analysis.ParseErrors);
    }

    [Fact]
    public void Parse_ReadsNestedActivities()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <Sequence DisplayName="Root">
            <Sequence.Activities>
              <TryCatch DisplayName="Try Work">
                <TryCatch.Try>
                  <Sequence DisplayName="Try Body">
                    <Sequence.Activities>
                      <ui:Click DisplayName="Click Login" />
                      <If DisplayName="Needs Retry" />
                    </Sequence.Activities>
                  </Sequence>
                </TryCatch.Try>
              </TryCatch>
            </Sequence.Activities>
          </Sequence>
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        Assert.Equal(["Sequence", "TryCatch", "Sequence", "Click", "If"], analysis.Activities.Select(activity => activity.Name));
    }

    [Fact]
    public void Parse_CalculatesDepth()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <If>
                <If.Then>
                  <Sequence>
                    <Sequence.Activities>
                      <ui:LogMessage />
                    </Sequence.Activities>
                  </Sequence>
                </If.Then>
              </If>
            </Sequence.Activities>
          </Sequence>
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        Assert.Equal([0, 1, 2, 3], analysis.Activities.Select(activity => activity.Depth));
    }

    [Fact]
    public void Parse_SetsParentActivityIds()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click />
              <ui:TypeInto />
            </Sequence.Activities>
          </Sequence>
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        var parent = analysis.Activities.Single(activity => activity.Name == "Sequence");
        var children = analysis.Activities.Where(activity => activity.ParentActivityId == parent.ActivityId).ToList();
        Assert.Equal(2, children.Count);
        Assert.All(children, child => Assert.Equal(1, child.Depth));
    }

    [Fact]
    public void Parse_ReadsDisplayName()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click DisplayName="Click Login Button" />
            </Sequence.Activities>
          </Sequence>
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        Assert.Equal("Click Login Button", analysis.Activities.Single(activity => activity.Name == "Click").DisplayName);
    }

    [Fact]
    public void Parse_KeepsGenericCustomActivities()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <custom:CompanySpecificActivity DisplayName="Company Step" CustomValue="42" />
            </Sequence.Activities>
          </Sequence>
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        var activity = analysis.Activities.Single(activity => activity.Name == "CompanySpecificActivity");
        Assert.Equal("Company Step", activity.DisplayName);
        Assert.Equal("42", activity.Properties["CustomValue"]);
    }

    [Fact]
    public void Parse_ReadsInvokeWorkflowFileProperty()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:InvokeWorkflowFile WorkflowFileName="Framework\InitAllSettings.xaml" />
            </Sequence.Activities>
          </Sequence>
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        var activity = analysis.Activities.Single(activity => activity.Name == "InvokeWorkflowFile");
        Assert.Equal(@"Framework\InitAllSettings.xaml", activity.Properties["WorkflowFileName"]);
    }

    [Fact]
    public void Parse_ReadsDelayDurationProperty()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <Delay Duration="00:00:05" />
            </Sequence.Activities>
          </Sequence>
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        var activity = analysis.Activities.Single(activity => activity.Name == "Delay");
        Assert.Equal("00:00:05", activity.Properties["Duration"]);
    }

    [Fact]
    public void Parse_TreatsTargetAsActivityPropertyMetadata()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click DisplayName="Click Login">
                <ui:Click.Target>
                  <ui:Target ClippingRegion="{x:Null}" Element="{x:Null}" Id="target-1" Selector="&lt;webctrl tag='BUTTON' idx='3' /&gt;" />
                </ui:Click.Target>
              </ui:Click>
            </Sequence.Activities>
          </Sequence>
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        Assert.Equal(["Sequence", "Click"], analysis.Activities.Select(activity => activity.Name));
        var click = analysis.Activities.Single(activity => activity.Name == "Click");
        Assert.Equal("<webctrl tag='BUTTON' idx='3' />", click.Properties["Selector"]);
    }

    [Fact]
    public void Parse_ReadsWorkflowArguments()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <x:Members>
            <x:Property Name="in_Config" Type="InArgument(scg:Dictionary(x:String, x:Object))" />
            <x:Property Name="out_Result" Type="OutArgument(x:String)" />
            <x:Property Name="io_TransactionData" Type="InOutArgument(x:Object)" />
          </x:Members>
          <Sequence />
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        Assert.Collection(
            analysis.Arguments,
            argument =>
            {
                Assert.Equal("in_Config", argument.Name);
                Assert.Equal("In", argument.Direction);
            },
            argument =>
            {
                Assert.Equal("out_Result", argument.Name);
                Assert.Equal("Out", argument.Direction);
            },
            argument =>
            {
                Assert.Equal("io_TransactionData", argument.Name);
                Assert.Equal("InOut", argument.Direction);
            });
    }

    private static string WorkflowXaml(string body)
    {
        return $$"""
        <Activity
          xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:ui="http://schemas.uipath.com/workflow/activities"
          xmlns:custom="http://schemas.company.com/rpa"
          xmlns:scg="clr-namespace:System.Collections.Generic;assembly=System.Private.CoreLib"
          x:Class="Main">
        {{body}}
        </Activity>
        """;
    }
}

file sealed class XamlParserTestProject : IDisposable
{
    public string RootPath { get; } = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantXamlTests-{Guid.NewGuid():N}");

    private XamlParserTestProject()
    {
        Directory.CreateDirectory(RootPath);
    }

    public static XamlParserTestProject Create()
    {
        return new XamlParserTestProject();
    }

    public string WriteXaml(string relativePath, string content)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}

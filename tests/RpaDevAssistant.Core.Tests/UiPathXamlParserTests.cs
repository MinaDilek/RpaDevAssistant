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
    public void Parse_ReadsInvokeWorkflowArgumentMappingsWithoutDictionaryActivity()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:InvokeWorkflowFile WorkflowFileName="Business\Process.xaml">
                <ui:InvokeWorkflowFile.Arguments>
                  <scg:Dictionary x:TypeArguments="x:String, Argument">
                    <InArgument x:Key="in_Config">[Config]</InArgument>
                    <OutArgument x:Key="out_Result">[processResult]</OutArgument>
                  </scg:Dictionary>
                </ui:InvokeWorkflowFile.Arguments>
              </ui:InvokeWorkflowFile>
            </Sequence.Activities>
          </Sequence>
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        var invoke = analysis.Activities.Single(activity => activity.Name == "InvokeWorkflowFile");
        Assert.Equal("[Config]", invoke.Arguments["in_Config"]);
        Assert.Equal("[processResult]", invoke.Arguments["out_Result"]);
        Assert.Equal("In", invoke.ArgumentMappingDirections["in_Config"]);
        Assert.Equal("Out", invoke.ArgumentMappingDirections["out_Result"]);
        Assert.DoesNotContain(analysis.Activities, activity => activity.Name == "Dictionary");
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
    public void Parse_PreservesCatchTypeArguments()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <TryCatch>
            <TryCatch.Catches>
              <Catch x:TypeArguments="ui:BusinessRuleException" />
            </TryCatch.Catches>
          </TryCatch>
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        var activity = analysis.Activities.Single(activity => activity.Name == "Catch");
        Assert.Equal("ui:BusinessRuleException", activity.Properties["TypeArguments"]);
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
    public void Parse_ReadsNestedClickSelectorInArgumentValue()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click DisplayName="Login Button">
                <ui:Click.Selector>
                  <InArgument x:TypeArguments="x:String">
                    <![CDATA[<webctrl id='btnLogin' tag='BUTTON' />]]>
                  </InArgument>
                </ui:Click.Selector>
              </ui:Click>
            </Sequence.Activities>
          </Sequence>
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        var click = analysis.Activities.Single(activity => activity.Name == "Click");
        Assert.True(click.Properties.TryGetValue("Selector", out var selector));
        Assert.Equal("<webctrl id='btnLogin' tag='BUTTON' />", selector);
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

    [Fact]
    public void Parse_PreservesSerializedArgumentDefaultsAndDistinguishesEmptyBindings()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <x:Members>
            <x:Property Name="in_AttributeDefault" Type="InArgument(x:String)" Default="attribute-value" />
            <x:Property Name="in_ElementDefault" Type="InArgument(x:String)">
              <x:Property.Default>
                <InArgument x:TypeArguments="x:String">["element-value"]</InArgument>
              </x:Property.Default>
            </x:Property>
            <x:Property Name="in_BoundDefault" Type="InArgument(x:String)" />
            <x:Property Name="in_NullDefault" Type="InArgument(x:String)" />
            <x:Property Name="in_NoDefault" Type="InArgument(x:String)" />
          </x:Members>
          <this:Main.in_BoundDefault xmlns:this="clr-namespace:">
            <InArgument x:TypeArguments="x:String">["bound-value"]</InArgument>
          </this:Main.in_BoundDefault>
          <this:Main.in_NullDefault xmlns:this="clr-namespace:">
            <InArgument x:TypeArguments="x:String"><x:Null /></InArgument>
          </this:Main.in_NullDefault>
          <this:Main.in_NoDefault xmlns:this="clr-namespace:">
            <InArgument x:TypeArguments="x:String" />
          </this:Main.in_NoDefault>
          <Sequence />
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        Assert.Equal("attribute-value", analysis.Arguments.Single(argument => argument.Name == "in_AttributeDefault").DefaultValue);
        Assert.Equal("[\"element-value\"]", analysis.Arguments.Single(argument => argument.Name == "in_ElementDefault").DefaultValue);
        Assert.Equal("[\"bound-value\"]", analysis.Arguments.Single(argument => argument.Name == "in_BoundDefault").DefaultValue);
        Assert.Equal("{x:Null}", analysis.Arguments.Single(argument => argument.Name == "in_NullDefault").DefaultValue);
        Assert.All(analysis.Arguments.Where(argument => argument.Name != "in_NoDefault"), argument => Assert.True(argument.HasDefaultValue));
        Assert.False(analysis.Arguments.Single(argument => argument.Name == "in_NoDefault").HasDefaultValue);
    }

    [Fact]
    public void Parse_ReadsVariablesWithoutCountingThemAsActivities()
    {
        using var project = XamlParserTestProject.Create();
        var xamlPath = project.WriteXaml("Main.xaml", WorkflowXaml("""
          <Sequence DisplayName="Main Sequence" IdRef="Sequence_1">
            <Sequence.Variables>
              <Variable x:TypeArguments="x:String" Name="customerName" Default="Sample" />
              <Variable x:TypeArguments="x:Int32" Name="Retry_Count">
                <Variable.Default>3</Variable.Default>
              </Variable>
            </Sequence.Variables>
          </Sequence>
        """));

        var analysis = new UiPathXamlParser().Parse(xamlPath, project.RootPath);

        Assert.Collection(
            analysis.Variables,
            variable =>
            {
                Assert.Equal("customerName", variable.Name);
                Assert.Equal("x:String", variable.Type);
                Assert.Equal("Sample", variable.DefaultValue);
                Assert.Equal("Main Sequence", variable.Scope);
                Assert.Equal("Sequence_1", variable.ScopeActivityId);
            },
            variable =>
            {
                Assert.Equal("Retry_Count", variable.Name);
                Assert.Equal("x:Int32", variable.Type);
                Assert.Equal("3", variable.DefaultValue);
                Assert.Equal("Sequence_1", variable.ScopeActivityId);
            });
        Assert.DoesNotContain(analysis.Activities, activity => activity.Name == "Variable");
        var sequence = Assert.Single(analysis.Activities);
        Assert.DoesNotContain("Name", sequence.Properties.Keys);
        Assert.DoesNotContain("Default", sequence.Properties.Keys);
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

using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Flowcharts;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Localization;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Parsing;
using RpaDevAssistant.Core.ProjectAssistant;
using RpaDevAssistant.Core.Reporting;
using RpaDevAssistant.Core.Reporting.Export;
using RpaDevAssistant.Core.Scanning;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathFlowchartConversionTests
{
    [Fact]
    public void Parser_DetectsFlowchartAndDoesNotTreatSequenceAsFlowchart()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart());
        fixture.WriteWorkflow("Main.xaml", Sequence());

        var scan = Scanner().Scan(fixture.RootPath);

        Assert.Equal(UiPathWorkflowStructureType.Flowchart, Workflow(scan, "Flow.xaml").Analysis?.StructureType);
        Assert.Equal(UiPathWorkflowStructureType.Sequence, Workflow(scan, "Main.xaml").Analysis?.StructureType);
    }

    [Fact]
    public void Parser_DetectsNestedFlowchartWithoutChangingRootStructure()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Nested.xaml", SequenceWithNestedFlowchart());

        var workflow = Workflow(Scanner().Scan(fixture.RootPath), "Nested.xaml");

        Assert.Equal(UiPathWorkflowStructureType.Sequence, workflow.Analysis?.StructureType);
        Assert.True(workflow.Analysis?.ContainsFlowchart);
        Assert.Equal(1, workflow.Analysis?.FlowchartCount);
    }

    [Fact]
    public void ProjectFlowchartSummary_IncludesNestedFlowchartWorkflowsAsNotSupported()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("RootFlow.xaml", LinearFlowchart());
        fixture.WriteWorkflow("Nested.xaml", SequenceWithNestedFlowchart());

        var summary = Scanner().Scan(fixture.RootPath).FlowchartAnalysis!;

        Assert.Equal(2, summary.FlowchartWorkflowCount);
        Assert.Equal(1, summary.RootFlowchartWorkflowCount);
        Assert.Equal(1, summary.NestedFlowchartWorkflowCount);
        Assert.Equal(2, summary.TotalFlowchartCount);
        Assert.Contains(summary.Workflows, workflow =>
            workflow.WorkflowPath == "Nested.xaml"
            && workflow.ContainsFlowchart
            && !workflow.IsRootFlowchart
            && workflow.ConversionLevel == UiPathFlowchartConversionLevel.NotSupported);
    }

    [Fact]
    public async Task LinearFlowchart_IsSafeAndPreviewKeepsOrder()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart());

        var result = await Service().AnalyzeAsync(fixture.RootPath, "Flow.xaml");

        Assert.Equal(UiPathFlowchartConversionLevel.Safe, result.Assessment?.ConversionLevel);
        Assert.Equal(new[] { "A", "B", "C" }, result.Plan!.PreviewTree!.Children.Select(node => node.SourceNodeId).ToArray());
        Assert.Equal(3, result.Plan.Mappings.Count);
    }

    [Fact]
    public async Task FlowDecision_BuildsIfPreviewWithTrueAndFalseBranches()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Decision.xaml", DecisionFlowchart());

        var result = await Service().AnalyzeAsync(fixture.RootPath, "Decision.xaml");
        var ifNode = Assert.Single(result.Plan!.PreviewTree!.Children);

        Assert.Equal("If", ifNode.Type);
        Assert.Equal("x > 0", ifNode.Condition);
        Assert.Contains(ifNode.Children, child => child.Type == "Then");
        Assert.Contains(ifNode.Children, child => child.Type == "Else");
    }

    [Fact]
    public async Task MergeNode_IsContinuationAndIsNotDuplicatedInsideBranches()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Merge.xaml", DecisionMergeFlowchart());

        var result = await Service().AnalyzeAsync(fixture.RootPath, "Merge.xaml");
        var top = result.Plan!.PreviewTree!.Children;

        Assert.Equal("If", top[0].Type);
        Assert.Equal("LogMessage", top[1].Type);
        Assert.Equal("C", top[1].SourceNodeId);
    }

    [Fact]
    public async Task NestedDecision_ProducesNestedIfPreview()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Nested.xaml", NestedDecisionFlowchart());

        var result = await Service().AnalyzeAsync(fixture.RootPath, "Nested.xaml");
        var outer = Assert.Single(result.Plan!.PreviewTree!.Children);
        var then = outer.Children.Single(child => child.Type == "Then");

        Assert.Equal("If", outer.Type);
        Assert.Contains(then.Children, child => child.Type == "If");
    }

    [Fact]
    public async Task FlowSwitch_ProducesSwitchPreview()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Switch.xaml", SwitchFlowchart());

        var result = await Service().AnalyzeAsync(fixture.RootPath, "Switch.xaml");
        var node = Assert.Single(result.Plan!.PreviewTree!.Children);

        Assert.Equal("Switch", node.Type);
        Assert.Contains(node.Children, child => child.Type == "Case");
    }

    [Fact]
    public async Task Cycle_IsDetectedAsComplexRetryConversionCandidate()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Cycle.xaml", CycleFlowchart());

        var result = await Service().AnalyzeAsync(fixture.RootPath, "Cycle.xaml");

        Assert.True(result.Graph!.HasCycles);
        Assert.Equal(UiPathFlowchartConversionLevel.Complex, result.Assessment!.ConversionLevel);
        Assert.True(result.Assessment.IsConvertible);
        Assert.Contains(result.Assessment.Risks, item => item.Contains("Cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UnreachableNode_IsDetectedAsReviewRisk()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Unreachable.xaml", UnreachableFlowchart());

        var result = await Service().AnalyzeAsync(fixture.RootPath, "Unreachable.xaml");

        Assert.True(result.Graph!.HasUnreachableNodes);
        Assert.Contains(result.Assessment!.Risks, risk => risk.Contains("Unreachable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InvokeWorkflowInsideBranch_IsPreservedInPreview()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Invoke.xaml", InvokeFlowchart());

        var result = await Service().AnalyzeAsync(fixture.RootPath, "Invoke.xaml");

        Assert.Contains(result.Graph!.Nodes, node => node.ActivityName == "InvokeWorkflowFile");
        Assert.Contains(result.Plan!.PreviewTree!.Children, node => ContainsPreviewType(node, "InvokeWorkflowFile"));
    }

    [Fact]
    public async Task Plan_PreservesArgumentsVariablesPropertiesAndExpressions()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart());

        var result = await Service().AnalyzeAsync(fixture.RootPath, "Flow.xaml");

        Assert.Contains("Arguments", result.Plan!.PreservedItems);
        Assert.Contains("Variables", result.Plan.PreservedItems);
        Assert.Contains("Activity properties", result.Plan.PreservedItems);
        Assert.Contains("Expressions", result.Plan.PreservedItems);
    }

    [Fact]
    public async Task AskProject_AnswersFlowchartQuestionsLocally()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart());
        fixture.WriteWorkflow("Main.xaml", Sequence());

        var answer = await QuestionService(fixture.RootPath).AskAsync(new UiPathProjectQuestion
        {
            ProjectPath = fixture.RootPath,
            Question = "Hangi workflow'lar Flowchart kullanıyor?",
            Locale = "tr"
        }, CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("Flowchart", answer.Answer);
    }

    [Fact]
    public void HtmlReport_IncludesFlowchartSummary()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart());
        var scan = Scanner().Scan(fixture.RootPath);
        var analysis = new UiPathStaticAnalysisResult();
        var score = new UiPathQualityScore { Score = 100, Grade = "A", ProfileId = "default", ProfileName = "Default" };
        var report = new UiPathAnalysisReportBuilder().Build(scan, analysis, score, new BuiltInUiPathRuleProfileProvider().GetProfile(null));

        var html = new HtmlUiPathReportExporter(new RpaDevAssistantLocalizer()).Export(report, "en").Content;

        Assert.Contains("Flowchart Analysis", html);
        Assert.Contains("Flow.xaml", html);
    }

    [Fact]
    public async Task ConversionPreview_DoesNotWriteWorkflowFile()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart());
        var before = File.ReadAllText(Path.Combine(fixture.RootPath, "Flow.xaml"));

        _ = await Service().AnalyzeAsync(fixture.RootPath, "Flow.xaml");

        Assert.Equal(before, File.ReadAllText(Path.Combine(fixture.RootPath, "Flow.xaml")));
    }

    [Fact]
    public async Task StandaloneConverter_AnalyzesSingleFlowchartWithoutProjectJsonContext()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Standalone.xaml", LinearFlowchart());
        var sourcePath = Path.Combine(fixture.RootPath, "Standalone.xaml");

        var result = await StandaloneConverter().AnalyzeAsync(sourcePath);

        Assert.Equal("Standalone", result.Context);
        Assert.Equal("Ready", result.Status);
        Assert.True(result.CanConvert);
        Assert.Equal(UiPathWorkflowStructureType.Flowchart, result.StructureType);
        Assert.Equal("Standalone_Sequence.xaml", result.SuggestedOutputFileName);
        Assert.NotNull(result.Plan?.PreviewTree);
    }

    [Fact]
    public async Task StandaloneConverter_ReportsSequenceAsAlreadySequence()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("AlreadySequence.xaml", Sequence());

        var result = await StandaloneConverter().AnalyzeAsync(Path.Combine(fixture.RootPath, "AlreadySequence.xaml"));

        Assert.Equal("AlreadySequence", result.Status);
        Assert.False(result.CanConvert);
        Assert.Contains(result.Messages, message => message.Contains("Sequence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StandaloneConverter_PreviewsNestedFlowchartAsConvertibleWhenSafe()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Nested.xaml", SequenceWithNestedFlowchart());

        var result = await StandaloneConverter().AnalyzeAsync(Path.Combine(fixture.RootPath, "Nested.xaml"));

        Assert.Equal("Ready", result.Status);
        Assert.True(result.CanConvert);
        Assert.Equal(UiPathWorkflowStructureType.Sequence, result.StructureType);
        Assert.NotNull(result.Plan?.PreviewTree);
        Assert.Contains(result.Messages, message => message.Contains("Nested Flowchart", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StandaloneConverter_SavesNestedFlowchartInsideNewSequenceFile()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Nested.xaml", SequenceWithNestedFlowchart());
        var sourcePath = Path.Combine(fixture.RootPath, "Nested.xaml");
        var outputPath = Path.Combine(fixture.RootPath, "Nested_Sequence.xaml");
        var original = File.ReadAllText(sourcePath);
        var analysis = await StandaloneConverter().AnalyzeAsync(sourcePath);

        var result = await StandaloneConverter().ConvertAsync(new UiPathStandaloneFlowchartConvertRequest
        {
            XamlFilePath = sourcePath,
            OutputPath = outputPath,
            ExpectedWorkflowHash = analysis.WorkflowHash,
            Confirmed = true
        });

        Assert.True(result.Success);
        Assert.True(result.Saved);
        Assert.Equal(original, File.ReadAllText(sourcePath));
        Assert.True(File.Exists(outputPath));
        var converted = File.ReadAllText(outputPath);
        Assert.Contains("Nested Flowchart Wrapper", converted);
        Assert.DoesNotContain("<Flowchart", converted);
        Assert.Equal(UiPathWorkflowStructureType.Sequence, new UiPathXamlParser().Parse(outputPath, fixture.RootPath).StructureType);
    }

    [Fact]
    public async Task StandaloneConverter_SavesRetryLoopAsWhileInNewFile()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Retry.xaml", RetryLoopFlowchart());
        var sourcePath = Path.Combine(fixture.RootPath, "Retry.xaml");
        var outputPath = Path.Combine(fixture.RootPath, "Retry_Sequence.xaml");
        var original = File.ReadAllText(sourcePath);
        var analysis = await StandaloneConverter().AnalyzeAsync(sourcePath);

        var result = await StandaloneConverter().ConvertAsync(new UiPathStandaloneFlowchartConvertRequest
        {
            XamlFilePath = sourcePath,
            OutputPath = outputPath,
            ExpectedWorkflowHash = analysis.WorkflowHash,
            Confirmed = true
        });

        Assert.Equal("Complex", analysis.Status);
        Assert.True(analysis.CanConvert);
        Assert.True(result.Success);
        Assert.True(result.Saved);
        Assert.Equal(original, File.ReadAllText(sourcePath));
        var converted = File.ReadAllText(outputPath);
        Assert.Contains("<While", converted);
        Assert.Contains("retryCount", converted);
        Assert.Equal(UiPathWorkflowStructureType.Sequence, new UiPathXamlParser().Parse(outputPath, fixture.RootPath).StructureType);
    }

    [Fact]
    public async Task StandaloneConverter_SavesConvertedWorkflowAsNewFileAndKeepsOriginal()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart());
        var sourcePath = Path.Combine(fixture.RootPath, "Flow.xaml");
        var outputPath = Path.Combine(fixture.RootPath, "Flow_Sequence.xaml");
        var original = File.ReadAllText(sourcePath);
        var analysis = await StandaloneConverter().AnalyzeAsync(sourcePath);

        var result = await StandaloneConverter().ConvertAsync(new UiPathStandaloneFlowchartConvertRequest
        {
            XamlFilePath = sourcePath,
            OutputPath = outputPath,
            ExpectedWorkflowHash = analysis.WorkflowHash,
            Confirmed = true
        });

        Assert.True(result.Success);
        Assert.True(result.Saved);
        Assert.Equal(original, File.ReadAllText(sourcePath));
        Assert.True(File.Exists(outputPath));
        Assert.Equal(UiPathWorkflowStructureType.Sequence, new UiPathXamlParser().Parse(outputPath, fixture.RootPath).StructureType);
    }

    [Fact]
    public async Task StandaloneConverter_ExcludesCommentedOutActivityBlocksFromPreviewAndOutput()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Commented.xaml", FlowchartWithCommentedOutBlock());
        var sourcePath = Path.Combine(fixture.RootPath, "Commented.xaml");
        var outputPath = Path.Combine(fixture.RootPath, "Commented_Sequence.xaml");
        var analysis = await StandaloneConverter().AnalyzeAsync(sourcePath);

        Assert.DoesNotContain(analysis.Plan!.PreviewTree!.Children, node =>
            node.Type.Contains("CommentOut", StringComparison.OrdinalIgnoreCase)
            || node.DisplayName?.Contains("Disabled", StringComparison.OrdinalIgnoreCase) == true);

        var result = await StandaloneConverter().ConvertAsync(new UiPathStandaloneFlowchartConvertRequest
        {
            XamlFilePath = sourcePath,
            OutputPath = outputPath,
            ExpectedWorkflowHash = analysis.WorkflowHash,
            Confirmed = true
        });

        Assert.True(result.Success);
        var converted = File.ReadAllText(outputPath);
        Assert.DoesNotContain("CommentOut", converted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Disabled Assignment", converted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Active Assignment", converted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StandaloneConverter_CompletelyExcludesUiCommentsCommentOutAndXmlComments()
    {
        using var fixture = new FlowchartProjectFixture();
        var xaml = $$"""
            <Flowchart xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"
                       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                       xmlns:ui="http://schemas.uipath.com/workflow/activities"
                       StartNode="{x:Reference A}">
              <FlowStep x:Name="A">
                <Sequence DisplayName="Main step">
                  <!-- An XML comment inside sequence -->
                  <Assign DisplayName="Step 1" />
                  <ui:Comment Text="A developer comment note" />
                  <Comment DisplayName="Another note" />
                </Sequence>
                <FlowStep.Next><x:Reference Name="B" /></FlowStep.Next>
              </FlowStep>
              <FlowStep x:Name="B">
                <ui:CommentOut DisplayName="Disabled FlowStep">
                  <ui:CommentOut.Body>
                    <ActivityAction x:TypeArguments="Activity">
                      <Assign DisplayName="Disabled Assign" />
                    </ActivityAction>
                  </ui:CommentOut.Body>
                </ui:CommentOut>
                <FlowStep.Next><x:Reference Name="C" /></FlowStep.Next>
              </FlowStep>
              <FlowStep x:Name="C">
                <Assign DisplayName="// Commented out step by prefix" />
                <FlowStep.Next><x:Reference Name="D" /></FlowStep.Next>
              </FlowStep>
              <FlowStep x:Name="D">
                <LogMessage DisplayName="Final Step" />
              </FlowStep>
            </Flowchart>
            """;

        fixture.WriteWorkflow("CommentsExclusion.xaml", xaml);
        var sourcePath = Path.Combine(fixture.RootPath, "CommentsExclusion.xaml");
        var outputPath = Path.Combine(fixture.RootPath, "CommentsExclusion_Sequence.xaml");
        var analysis = await StandaloneConverter().AnalyzeAsync(sourcePath);

        Assert.True(analysis.CanConvert);
        Assert.DoesNotContain(analysis.Plan!.PreviewTree!.Children, node =>
            node.Type.Contains("Comment", StringComparison.OrdinalIgnoreCase)
            || node.DisplayName?.Contains("Disabled", StringComparison.OrdinalIgnoreCase) == true
            || node.DisplayName?.StartsWith("//", StringComparison.Ordinal) == true);

        var result = await StandaloneConverter().ConvertAsync(new UiPathStandaloneFlowchartConvertRequest
        {
            XamlFilePath = sourcePath,
            OutputPath = outputPath,
            ExpectedWorkflowHash = analysis.WorkflowHash,
            Confirmed = true
        });

        Assert.True(result.Success);
        var converted = File.ReadAllText(outputPath);
        Assert.DoesNotContain("CommentOut", converted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ui:Comment", converted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<Comment", converted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Disabled FlowStep", converted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Disabled Assign", converted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("An XML comment inside sequence", converted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Commented out step by prefix", converted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Step 1", converted, StringComparison.Ordinal);
        Assert.Contains("Final Step", converted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StandaloneConverter_ResolvesStartNodeElement_CopiesVariables_AndExcludesCommentOut()
    {
        using var fixture = new FlowchartProjectFixture();
        var xaml = $$"""
            <Activity mc:Ignorable="sap sap2010" x:Class="Service_Test"
                      xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"
                      xmlns:av="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                      xmlns:sap="http://schemas.microsoft.com/netfx/2009/xaml/activities/presentation"
                      xmlns:sap2010="http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation"
                      xmlns:ui="http://schemas.uipath.com/workflow/activities"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Sequence DisplayName="RootSequence">
                <Flowchart DisplayName="Services_Flowchart">
                  <Flowchart.Variables>
                    <Variable x:TypeArguments="x:Int32" Name="retry_count" />
                    <Variable x:TypeArguments="x:String" Name="result_sorgu" />
                  </Flowchart.Variables>
                  <Flowchart.StartNode>
                    <x:Reference>__ReferenceIDStart</x:Reference>
                  </Flowchart.StartNode>
                  <FlowStep x:Name="__ReferenceIDLoopStep">
                    <ui:CommentOut sap2010:WorkflowViewState.IdRef="CommentOut_1">
                      <ui:CommentOut.Body>
                        <Sequence DisplayName="Ignored Activities">
                          <Assign DisplayName="in_Services_Get_Timeout" />
                        </Sequence>
                      </ui:CommentOut.Body>
                    </ui:CommentOut>
                    <FlowStep.Next>
                      <FlowStep x:Name="__ReferenceIDHttp">
                        <ui:HttpClient DisplayName="HTTP Request Get" Method="GET" />
                        <FlowStep.Next>
                          <FlowStep x:Name="__ReferenceIDJson">
                            <ui:DeserializeJson DisplayName="Deserialize JSON" JsonString="[result_sorgu]" />
                          </FlowStep>
                        </FlowStep.Next>
                      </FlowStep>
                    </FlowStep.Next>
                  </FlowStep>
                  <FlowStep x:Name="__ReferenceIDStart">
                    <Assign DisplayName="retry_count=1">
                      <Assign.To>
                        <OutArgument x:TypeArguments="x:Int32">[retry_count]</OutArgument>
                      </Assign.To>
                      <Assign.Value>
                        <InArgument x:TypeArguments="x:Int32">1</InArgument>
                      </Assign.Value>
                    </Assign>
                    <FlowStep.Next>
                      <x:Reference>__ReferenceIDLoopStep</x:Reference>
                    </FlowStep.Next>
                  </FlowStep>
                </Flowchart>
              </Sequence>
            </Activity>
            """;

        fixture.WriteWorkflow("Services_Test.xaml", xaml);
        var sourcePath = Path.Combine(fixture.RootPath, "Services_Test.xaml");
        var outputPath = Path.Combine(fixture.RootPath, "Services_Test_Sequence.xaml");
        var analysis = await StandaloneConverter().AnalyzeAsync(sourcePath);

        Assert.True(analysis.CanConvert);
        Assert.Equal("__ReferenceIDStart", analysis.Graph?.StartNodeId);

        var result = await StandaloneConverter().ConvertAsync(new UiPathStandaloneFlowchartConvertRequest
        {
            XamlFilePath = sourcePath,
            OutputPath = outputPath,
            ExpectedWorkflowHash = analysis.WorkflowHash,
            Confirmed = true
        });

        Assert.True(result.Success);
        var converted = File.ReadAllText(outputPath);

        Assert.DoesNotContain("Review Required - Unmapped Flowchart Nodes", converted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("retry_count=1", converted, StringComparison.Ordinal);

        Assert.Contains("<Sequence.Variables>", converted, StringComparison.Ordinal);
        Assert.Contains("Name=\"retry_count\"", converted, StringComparison.Ordinal);
        Assert.Contains("Name=\"result_sorgu\"", converted, StringComparison.Ordinal);

        Assert.DoesNotContain("CommentOut", converted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("in_Services_Get_Timeout", converted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ignored Activities", converted, StringComparison.OrdinalIgnoreCase);

        var projectJson = File.ReadAllText(Path.Combine(fixture.RootPath, "project.json"));
        Assert.Contains("UiPath.WebAPI.Activities", projectJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StandaloneConverter_DoesNotOverwriteOriginalFile()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart());
        var sourcePath = Path.Combine(fixture.RootPath, "Flow.xaml");

        var result = await StandaloneConverter().ConvertAsync(new UiPathStandaloneFlowchartConvertRequest
        {
            XamlFilePath = sourcePath,
            OutputPath = sourcePath,
            Confirmed = true
        });

        Assert.False(result.Success);
        Assert.False(result.Saved);
        Assert.Equal("invalid_output_path", result.ErrorCode);
    }

    [Fact]
    public async Task SafeLinearFlowchart_AppliesSuccessfullyWithBackupAndSequenceRoot()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart());
        var original = File.ReadAllText(Path.Combine(fixture.RootPath, "Flow.xaml"));
        var hash = UiPathFileHash.Sha256(Path.Combine(fixture.RootPath, "Flow.xaml"));

        var result = await ApplyService().ApplyAsync(new UiPathFlowchartConversionApplyRequest
        {
            ProjectPath = fixture.RootPath,
            WorkflowPath = "Flow.xaml",
            ExpectedWorkflowHash = hash,
            Confirmed = true
        });

        Assert.True(result.Success);
        Assert.True(result.Applied);
        Assert.True(result.RollbackAvailable);
        Assert.NotNull(result.BackupId);
        Assert.True(File.Exists(result.BackupPath));
        Assert.Equal(original, File.ReadAllText(result.BackupPath!));
        Assert.Equal(UiPathWorkflowStructureType.Sequence, new UiPathXamlParser().Parse(Path.Combine(fixture.RootPath, "Flow.xaml"), fixture.RootPath).StructureType);
        Assert.Contains("DisplayName=\"Prepare\"", File.ReadAllText(Path.Combine(fixture.RootPath, "Flow.xaml")));
    }

    [Fact]
    public async Task FlowDecision_AppliesToIfStructure()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Decision.xaml", DecisionFlowchart());

        var result = await ApplyService().ApplyAsync(ApplyRequest(fixture, "Decision.xaml"));
        var converted = File.ReadAllText(Path.Combine(fixture.RootPath, "Decision.xaml"));

        Assert.True(result.Success);
        Assert.Contains("<If", converted);
        Assert.Contains("Condition=\"x &gt; 0\"", converted);
        Assert.Contains("If.Then", converted);
        Assert.Contains("If.Else", converted);
        Assert.Equal(UiPathWorkflowStructureType.Sequence, new UiPathXamlParser().Parse(Path.Combine(fixture.RootPath, "Decision.xaml"), fixture.RootPath).StructureType);
    }

    [Fact]
    public async Task FlowSwitch_AppliesToSwitchStructure()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Switch.xaml", SwitchFlowchart());

        var result = await ApplyService().ApplyAsync(ApplyRequest(fixture, "Switch.xaml"));
        var converted = File.ReadAllText(Path.Combine(fixture.RootPath, "Switch.xaml"));

        Assert.True(result.Success);
        Assert.Contains("<Switch", converted);
        Assert.Contains("Expression=\"kind\"", converted);
        Assert.Contains("Switch.Case", converted);
    }

    [Fact]
    public async Task SharedContinuation_IsNotDuplicatedWhenApplied()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Merge.xaml", DecisionMergeFlowchart());

        var result = await ApplyService().ApplyAsync(ApplyRequest(fixture, "Merge.xaml"));
        var converted = File.ReadAllText(Path.Combine(fixture.RootPath, "Merge.xaml"));

        Assert.True(result.Success);
        Assert.Equal(1, CountOccurrences(converted, "DisplayName=\"Merged\""));
    }

    [Fact]
    public async Task ArgumentsVariablesAndInvokeReferences_ArePreservedWhenApplied()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Invoke.xaml", FlowchartWithArgumentsVariablesAndInvoke());

        var result = await ApplyService().ApplyAsync(ApplyRequest(fixture, "Invoke.xaml"));
        var converted = File.ReadAllText(Path.Combine(fixture.RootPath, "Invoke.xaml"));

        Assert.True(result.Success);
        Assert.Contains("x:Members", converted);
        Assert.Contains("in_Config", converted);
        Assert.Contains("Variable", converted);
        Assert.Contains("Business.xaml", converted);
    }

    [Fact]
    public async Task StaleWorkflowHash_BlocksApplyAndLeavesOriginalUnchanged()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart());
        var staleHash = UiPathFileHash.Sha256(Path.Combine(fixture.RootPath, "Flow.xaml"));
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart().Replace("Prepare", "Prepare Changed", StringComparison.Ordinal));
        var current = File.ReadAllText(Path.Combine(fixture.RootPath, "Flow.xaml"));

        var result = await ApplyService().ApplyAsync(new UiPathFlowchartConversionApplyRequest
        {
            ProjectPath = fixture.RootPath,
            WorkflowPath = "Flow.xaml",
            ExpectedWorkflowHash = staleHash,
            Confirmed = true
        });

        Assert.False(result.Success);
        Assert.Equal("stale_workflow_hash", result.ErrorCode);
        Assert.Equal(current, File.ReadAllText(Path.Combine(fixture.RootPath, "Flow.xaml")));
    }

    [Fact]
    public async Task Cycle_BlocksAutomaticApply()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Cycle.xaml", CycleFlowchart());
        var original = File.ReadAllText(Path.Combine(fixture.RootPath, "Cycle.xaml"));

        var result = await ApplyService().ApplyAsync(ApplyRequest(fixture, "Cycle.xaml"));

        Assert.False(result.Success);
        Assert.Equal("conversion_not_safe", result.ErrorCode);
        Assert.Equal(original, File.ReadAllText(Path.Combine(fixture.RootPath, "Cycle.xaml")));
    }

    [Fact]
    public async Task UnsupportedStructure_BlocksAutomaticApply()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Main.xaml", Sequence());

        var result = await ApplyService().ApplyAsync(ApplyRequest(fixture, "Main.xaml"));

        Assert.False(result.Success);
        Assert.Equal("preview_invalid", result.ErrorCode);
    }

    [Fact]
    public async Task NestedFlowchart_BlocksAutomaticApplyAndExplainsRootRequirement()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Nested.xaml", SequenceWithNestedFlowchart());
        var original = File.ReadAllText(Path.Combine(fixture.RootPath, "Nested.xaml"));

        var preview = await Service().AnalyzeAsync(fixture.RootPath, "Nested.xaml");
        var apply = await ApplyService().ApplyAsync(ApplyRequest(fixture, "Nested.xaml"));

        Assert.Contains(preview.Errors, error => error.Contains("nested Flowchart", StringComparison.OrdinalIgnoreCase));
        Assert.False(apply.Success);
        Assert.Equal("preview_invalid", apply.ErrorCode);
        Assert.Equal(original, File.ReadAllText(Path.Combine(fixture.RootPath, "Nested.xaml")));
    }

    [Fact]
    public async Task FailedGeneration_LeavesOriginalUnchangedAndDoesNotLeaveTempFile()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("EmptyStep.xaml", Flowchart("""
        <FlowStep x:Name="A" />
        """, "A"));
        var original = File.ReadAllText(Path.Combine(fixture.RootPath, "EmptyStep.xaml"));

        var result = await ApplyService().ApplyAsync(ApplyRequest(fixture, "EmptyStep.xaml"));

        Assert.False(result.Success);
        Assert.Equal("generation_failed", result.ErrorCode);
        Assert.Equal(original, File.ReadAllText(Path.Combine(fixture.RootPath, "EmptyStep.xaml")));
        Assert.Empty(Directory.EnumerateFiles(fixture.RootPath, "*.flowchart.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Confirmation_IsRequiredBeforeApply()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart());
        var original = File.ReadAllText(Path.Combine(fixture.RootPath, "Flow.xaml"));

        var result = await ApplyService().ApplyAsync(new UiPathFlowchartConversionApplyRequest
        {
            ProjectPath = fixture.RootPath,
            WorkflowPath = "Flow.xaml",
            ExpectedWorkflowHash = UiPathFileHash.Sha256(Path.Combine(fixture.RootPath, "Flow.xaml")),
            Confirmed = false
        });

        Assert.False(result.Success);
        Assert.Equal("invalid_request", result.ErrorCode);
        Assert.Equal(original, File.ReadAllText(Path.Combine(fixture.RootPath, "Flow.xaml")));
    }

    [Fact]
    public async Task Rollback_RestoresOriginalContentFromKnownConversionBackup()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart());
        var original = File.ReadAllText(Path.Combine(fixture.RootPath, "Flow.xaml"));
        var apply = await ApplyService().ApplyAsync(ApplyRequest(fixture, "Flow.xaml"));

        var rollback = await ApplyService().RollbackAsync(new UiPathFlowchartConversionRollbackRequest
        {
            ProjectPath = fixture.RootPath,
            WorkflowPath = "Flow.xaml",
            BackupId = apply.BackupId!,
            ExpectedCurrentHash = apply.ConvertedHash
        });

        Assert.True(rollback.Success);
        Assert.True(rollback.Restored);
        Assert.Equal(original, File.ReadAllText(Path.Combine(fixture.RootPath, "Flow.xaml")));
    }

    [Fact]
    public async Task Rollback_RejectsArbitraryOrNonConversionBackup()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("Flow.xaml", LinearFlowchart());
        var workflow = Path.Combine(fixture.RootPath, "Flow.xaml");
        var backup = new UiPathBackupService().CreateBackup(fixture.RootPath, "Flow.xaml", workflow, UiPathFileHash.Sha256(workflow), UiPathFileHash.Sha256(workflow), "RPA007", "DisplayName", "Click", "Click Login");

        var rollback = await ApplyService().RollbackAsync(new UiPathFlowchartConversionRollbackRequest
        {
            ProjectPath = fixture.RootPath,
            WorkflowPath = "Flow.xaml",
            BackupId = backup.BackupId
        });

        Assert.False(rollback.Success);
        Assert.Equal("backup_not_allowed", rollback.ErrorCode);
    }

    [Fact]
    public async Task Standalone_DetectsCustomDependencyActivitiesAndSuggestsStandardUiPathReplacements()
    {
        using var fixture = new FlowchartProjectFixture();
        fixture.WriteWorkflow("CustomFlow.xaml", FlowchartWithCustomActivities());

        var result = await StandaloneConverter().AnalyzeAsync(Path.Combine(fixture.RootPath, "CustomFlow.xaml"));

        Assert.True(result.CanConvert);
        Assert.NotNull(result.Plan);
        Assert.NotEmpty(result.CustomActivityDetections);
        Assert.Contains(result.CustomActivityDetections, d =>
            d.ActivityName == "CustomLogUtility"
            && d.SuggestedUiPathActivity == "ui:LogMessage"
            && d.SuggestedPackage == "UiPath.System.Activities");
        Assert.Contains(result.CustomActivityDetections, d =>
            d.ActivityName == "CustomHttpClient"
            && d.SuggestedUiPathActivity == "ui:HttpClient"
            && d.SuggestedPackage == "UiPath.WebAPI.Activities");
    }

    [Fact]
    public async Task Standalone_ReplacesCustomActivitiesWithUiPathStandardWhenConfirmed()
    {
        using var fixture = new FlowchartProjectFixture();
        var sourcePath = Path.Combine(fixture.RootPath, "CustomFlow.xaml");
        var outputPath = Path.Combine(fixture.RootPath, "Converted_Sequence.xaml");
        fixture.WriteWorkflow("CustomFlow.xaml", FlowchartWithCustomActivities());

        var convert = await StandaloneConverter().ConvertAsync(new UiPathStandaloneFlowchartConvertRequest
        {
            XamlFilePath = sourcePath,
            OutputPath = outputPath,
            Confirmed = true,
            ReplaceCustomActivitiesWithUiPathStandard = true
        });

        Assert.True(convert.Success);
        Assert.True(convert.Saved);
        Assert.True(File.Exists(outputPath));

        var outputXaml = File.ReadAllText(outputPath);
        Assert.Contains("ui:LogMessage", outputXaml);
        Assert.Contains("ui:HttpClient", outputXaml);
        Assert.DoesNotContain("CustomLogUtility", outputXaml);
        Assert.DoesNotContain("CustomHttpClient", outputXaml);
        Assert.Contains("xmlns:ui=\"http://schemas.uipath.com/workflow/activities\"", outputXaml);
    }

    [Fact]
    public async Task Standalone_ServiceFlow_ParsesHttpClientCorrectly()
    {
        using var fixture = new FlowchartProjectFixture();
        var path = Path.Combine(fixture.RootPath, "Services_Sorgu.xaml");
        var outputPath = Path.Combine(fixture.RootPath, "Services_Sorgu_Sequence.xaml");
        fixture.WriteWorkflow("Services_Sorgu.xaml", ServiceFlowchartWithHttpClient());

        var result = await StandaloneConverter().AnalyzeAsync(path);
        Assert.True(result.CanConvert);

        var convertResult = await StandaloneConverter().ConvertAsync(new UiPathStandaloneFlowchartConvertRequest
        {
            XamlFilePath = path,
            OutputPath = outputPath,
            ExpectedWorkflowHash = result.WorkflowHash,
            Confirmed = true,
            ReplaceCustomActivitiesWithUiPathStandard = true
        });

        Assert.True(convertResult.Success);
        var convertedXaml = File.ReadAllText(outputPath);
        Assert.Contains("ui:HttpClient", convertedXaml);
        Assert.Contains("HTTP Request Get", convertedXaml);
        Assert.Contains("EndPoint=", convertedXaml);
        Assert.Contains("<ui:HttpClient.Attachments>", convertedXaml);
        Assert.Contains("xmlns:ui=\"http://schemas.uipath.com/workflow/activities\"", convertedXaml);
        Assert.Contains("ui:DeserializeJson", convertedXaml);
        Assert.Contains("<Sequence.Variables>", convertedXaml);
        Assert.Contains("retry_count", convertedXaml);
        Assert.DoesNotContain("ui:CommentOut", convertedXaml);
        Assert.DoesNotContain("CommentOut_1", convertedXaml);
    }





    private static string ServiceFlowchartWithHttpClient() => """
        <Activity mc:Ignorable="sap sap2010" x:Class="Services_Sorgu"
                  xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"
                  xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                  xmlns:sap="http://schemas.microsoft.com/netfx/2009/xaml/activities/presentation"
                  xmlns:sap2010="http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation"
                  xmlns:scg="clr-namespace:System.Collections.Generic;assembly=System.Private.CoreLib"
                  xmlns:ui="http://schemas.uipath.com/workflow/activities"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Sequence DisplayName="Root Sequence">
            <Flowchart DisplayName="Services Flowchart">
              <Flowchart.Variables>
                <Variable x:TypeArguments="x:Int32" Name="retry_count" />
                <Variable x:TypeArguments="x:String" Name="result_sorgu" />
              </Flowchart.Variables>
              <Flowchart.StartNode><x:Reference>A</x:Reference></Flowchart.StartNode>
              <FlowStep x:Name="A">
                <ui:HttpClient DisplayName="HTTP Request Get" EndPoint="https://api.example.com/items" Method="GET">
                  <ui:HttpClient.Attachments>
                    <scg:Dictionary x:TypeArguments="x:String, InArgument(x:String)" />
                  </ui:HttpClient.Attachments>
                </ui:HttpClient>
                <FlowStep.Next><x:Reference>B</x:Reference></FlowStep.Next>
              </FlowStep>
              <FlowStep x:Name="B">
                <Sequence DisplayName="Process response">
                  <ui:DeserializeJson DisplayName="Deserialize JSON" JsonString="[result_sorgu]" />
                  <ui:CommentOut sap2010:WorkflowViewState.IdRef="CommentOut_1">
                    <ui:CommentOut.Body>
                      <Sequence DisplayName="Disabled"><Assign DisplayName="Ignored" /></Sequence>
                    </ui:CommentOut.Body>
                  </ui:CommentOut>
                </Sequence>
              </FlowStep>
            </Flowchart>
          </Sequence>
        </Activity>
        """;

    private static string FlowchartWithCustomActivities() => Flowchart("""
        <FlowStep x:Name="A">
          <CustomLogUtility DisplayName="Write Audit Log" />
          <FlowStep.Next><x:Reference Name="B" /></FlowStep.Next>
        </FlowStep>
        <FlowStep x:Name="B">
          <CustomHttpClient DisplayName="Fetch Data" Endpoint="https://api.example.com/items" />
        </FlowStep>
        """, "A");

    private static bool ContainsPreviewType(UiPathSequencePreviewNode node, string type)
    {
        return node.Type.Equals(type, StringComparison.OrdinalIgnoreCase)
            || node.Children.Any(child => ContainsPreviewType(child, type));
    }

    private static UiPathWorkflowInfo Workflow(ProjectScanResult scan, string path)
    {
        return scan.Workflows.Single(workflow => workflow.RelativePath == path);
    }

    private static UiPathProjectScanner Scanner()
    {
        return new UiPathProjectScanner();
    }

    private static UiPathFlowchartConversionService Service()
    {
        return new UiPathFlowchartConversionService(Scanner(), new UiPathFlowchartAnalyzer());
    }

    private static UiPathFlowchartConversionApplyService ApplyService()
    {
        var parser = new UiPathXamlParser();
        var scanner = Scanner();
        var eligibility = new UiPathUndoEligibilityService();
        var repository = new UiPathBackupRepository(eligibility);
        return new UiPathFlowchartConversionApplyService(
            Service(),
            parser,
            scanner,
            new UiPathBackupService(),
            repository,
            new UiPathBackupRestoreService(repository, parser, scanner, new UiPathRestoreAuditLogger()),
            new UiPathMutationLock());
    }

    private static UiPathStandaloneFlowchartConverter StandaloneConverter()
    {
        var parser = new UiPathXamlParser();
        return new UiPathStandaloneFlowchartConverter(parser, new UiPathFlowchartAnalyzer(), ApplyService());
    }

    private static UiPathFlowchartConversionApplyRequest ApplyRequest(FlowchartProjectFixture fixture, string workflowPath)
    {
        return new UiPathFlowchartConversionApplyRequest
        {
            ProjectPath = fixture.RootPath,
            WorkflowPath = workflowPath,
            ExpectedWorkflowHash = UiPathFileHash.Sha256(Path.Combine(fixture.RootPath, workflowPath)),
            Confirmed = true
        };
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static UiPathProjectQuestionService QuestionService(string projectPath)
    {
        var scan = Scanner().Scan(projectPath);
        var profile = new BuiltInUiPathRuleProfileProvider().GetProfile(null);
        var analyzerResult = new UiPathProjectAnalysisResult
        {
            ProjectScan = scan,
            Analysis = new UiPathStaticAnalysisResult(),
            QualityScore = new UiPathQualityScore { Score = 100, Grade = "A", ProfileId = profile.Id, ProfileName = profile.Name },
            Profile = profile
        };
        return new UiPathProjectQuestionService(
            new FakeAnalyzer(analyzerResult),
            new UiPathProjectQuestionClassifier(),
            new UiPathProjectRetriever(new SensitiveValueRedactor()),
            new UiPathWorkflowGraphBuilder(),
            new UiPathProjectAssistantPromptBuilder(),
            new NotConfiguredProjectAssistantAiProvider(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UiPathProjectQuestionService>.Instance,
            new RpaDevAssistantLocalizer());
    }

    private static string Sequence() => """
        <Sequence xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities" DisplayName="Main" />
        """;

    private static string LinearFlowchart() => Flowchart("""
        <FlowStep x:Name="A"><Assign DisplayName="Prepare" /><FlowStep.Next><x:Reference Name="B" /></FlowStep.Next></FlowStep>
        <FlowStep x:Name="B"><Delay DisplayName="Wait" /><FlowStep.Next><x:Reference Name="C" /></FlowStep.Next></FlowStep>
        <FlowStep x:Name="C"><LogMessage DisplayName="Done" />
        </FlowStep>
        """, "A");

    private static string FlowchartWithCommentedOutBlock() => Flowchart("""
        <FlowStep x:Name="A">
          <Sequence DisplayName="Mixed activity block">
            <Assign DisplayName="Active Assignment" />
            <CommentOut DisplayName="Disabled block">
              <CommentOut.Body>
                <Sequence DisplayName="Disabled activities">
                  <Assign DisplayName="Disabled Assignment" />
                </Sequence>
              </CommentOut.Body>
            </CommentOut>
          </Sequence>
          <FlowStep.Next><x:Reference Name="B" /></FlowStep.Next>
        </FlowStep>
        <FlowStep x:Name="B"><LogMessage DisplayName="Done" /></FlowStep>
        """, "A");

    private static string DecisionFlowchart() => Flowchart("""
        <FlowDecision x:Name="D" Condition="x > 0">
          <FlowDecision.True><x:Reference Name="A" /></FlowDecision.True>
          <FlowDecision.False><x:Reference Name="B" /></FlowDecision.False>
        </FlowDecision>
        <FlowStep x:Name="A"><Assign DisplayName="Positive" /></FlowStep>
        <FlowStep x:Name="B"><Assign DisplayName="Other" /></FlowStep>
        """, "D");

    private static string DecisionMergeFlowchart() => Flowchart("""
        <FlowDecision x:Name="D" Condition="ok">
          <FlowDecision.True><x:Reference Name="A" /></FlowDecision.True>
          <FlowDecision.False><x:Reference Name="B" /></FlowDecision.False>
        </FlowDecision>
        <FlowStep x:Name="A"><Assign DisplayName="True path" /><FlowStep.Next><x:Reference Name="C" /></FlowStep.Next></FlowStep>
        <FlowStep x:Name="B"><Assign DisplayName="False path" /><FlowStep.Next><x:Reference Name="C" /></FlowStep.Next></FlowStep>
        <FlowStep x:Name="C"><LogMessage DisplayName="Merged" /></FlowStep>
        """, "D");

    private static string NestedDecisionFlowchart() => Flowchart("""
        <FlowDecision x:Name="D1" Condition="a">
          <FlowDecision.True><x:Reference Name="D2" /></FlowDecision.True>
          <FlowDecision.False><x:Reference Name="C" /></FlowDecision.False>
        </FlowDecision>
        <FlowDecision x:Name="D2" Condition="b">
          <FlowDecision.True><x:Reference Name="A" /></FlowDecision.True>
          <FlowDecision.False><x:Reference Name="B" /></FlowDecision.False>
        </FlowDecision>
        <FlowStep x:Name="A"><Assign DisplayName="A" /></FlowStep>
        <FlowStep x:Name="B"><Assign DisplayName="B" /></FlowStep>
        <FlowStep x:Name="C"><Assign DisplayName="C" /></FlowStep>
        """, "D1");

    private static string SwitchFlowchart() => Flowchart("""
        <FlowSwitch x:Name="S" Expression="kind">
          <FlowSwitch.Cases>
            <FlowSwitch.Case Key="A"><x:Reference Name="A" /></FlowSwitch.Case>
            <FlowSwitch.Case Key="B"><x:Reference Name="B" /></FlowSwitch.Case>
          </FlowSwitch.Cases>
        </FlowSwitch>
        <FlowStep x:Name="A"><Assign DisplayName="A" /></FlowStep>
        <FlowStep x:Name="B"><Assign DisplayName="B" /></FlowStep>
        """, "S");

    private static string CycleFlowchart() => Flowchart("""
        <FlowStep x:Name="A"><Assign DisplayName="A" /><FlowStep.Next><x:Reference Name="B" /></FlowStep.Next></FlowStep>
        <FlowStep x:Name="B"><Assign DisplayName="B" /><FlowStep.Next><x:Reference Name="A" /></FlowStep.Next></FlowStep>
        """, "A");

    private static string RetryLoopFlowchart() => Flowchart("""
        <FlowDecision x:Name="CheckData" Condition="retryCount &lt; maxRetry">
          <FlowDecision.True><x:Reference Name="ReadData" /></FlowDecision.True>
          <FlowDecision.False><x:Reference Name="Done" /></FlowDecision.False>
        </FlowDecision>
        <FlowStep x:Name="ReadData"><Assign DisplayName="Read Data" /><FlowStep.Next><x:Reference Name="IncrementRetry" /></FlowStep.Next></FlowStep>
        <FlowStep x:Name="IncrementRetry"><Assign DisplayName="Increment Retry" /><FlowStep.Next><x:Reference Name="CheckData" /></FlowStep.Next></FlowStep>
        <FlowStep x:Name="Done"><LogMessage DisplayName="Done" /></FlowStep>
        """, "CheckData");

    private static string UnreachableFlowchart() => Flowchart("""
        <FlowStep x:Name="A"><Assign DisplayName="A" /></FlowStep>
        <FlowStep x:Name="B"><Assign DisplayName="B" /></FlowStep>
        """, "A");

    private static string InvokeFlowchart() => Flowchart("""
        <FlowStep x:Name="A"><InvokeWorkflowFile DisplayName="Invoke Business" WorkflowFileName="Business.xaml" /></FlowStep>
        """, "A");

    private static string FlowchartWithArgumentsVariablesAndInvoke() => """
        <Activity x:Class="Invoke"
                  xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <x:Members>
            <x:Property Name="in_Config" Type="InArgument(x:String)" />
          </x:Members>
          <Flowchart DisplayName="Invoke" StartNode="{x:Reference A}">
            <FlowStep x:Name="A">
              <Sequence DisplayName="Wrapper">
                <Sequence.Variables>
                  <Variable x:TypeArguments="x:String" Name="localValue" />
                </Sequence.Variables>
                <InvokeWorkflowFile DisplayName="Invoke Business" WorkflowFileName="Business.xaml" />
              </Sequence>
            </FlowStep>
          </Flowchart>
        </Activity>
        """;

    private static string SequenceWithNestedFlowchart() => $$"""
        <Sequence xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  DisplayName="Nested Flowchart Wrapper">
          {{Flowchart("""
          <FlowStep x:Name="A"><Assign DisplayName="Prepare" /></FlowStep>
          """, "A")}}
        </Sequence>
        """;

    private static string Flowchart(string body, string start) => $$"""
        <Flowchart xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"
                   xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                   StartNode="{x:Reference {{start}}}">
        {{body}}
        </Flowchart>
        """;

    private sealed class FlowchartProjectFixture : IDisposable
    {
        public FlowchartProjectFixture()
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"RpaFlowchartProject-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
            File.WriteAllText(Path.Combine(RootPath, "project.json"), """{"name":"FlowchartProject","dependencies":{"UiPath.System.Activities":"[24.10.6]"}}""");
        }

        public string RootPath { get; }

        public void WriteWorkflow(string relativePath, string xaml)
        {
            var fullPath = Path.Combine(RootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, xaml);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private sealed class NotConfiguredProjectAssistantAiProvider : IUiPathProjectAssistantAiProvider
    {
        public string ProviderName => "None";

        public bool IsConfigured => false;

        public Task<UiPathProjectAnswer> AnswerAsync(UiPathProjectAssistantPrompt prompt, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Flowchart questions should be answered locally.");
        }
    }

    private sealed class FakeAnalyzer : IUiPathProjectAnalyzer
    {
        private readonly UiPathProjectAnalysisResult result;

        public FakeAnalyzer(UiPathProjectAnalysisResult result)
        {
            this.result = result;
        }

        public UiPathProjectAnalysisResult Analyze(string projectPath, string? profileId = null)
        {
            return result;
        }
    }
}

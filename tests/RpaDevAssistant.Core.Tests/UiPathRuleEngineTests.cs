using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Rules;
using RpaDevAssistant.Core.Scanning;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathRuleEngineTests
{
    [Fact]
    public void Rpa001_ReturnsFinding_WhenDelayExists()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <Delay Duration="00:00:04" />
            </Sequence.Activities>
          </Sequence>
        """));

        var findings = AnalyzeRule(project, new AvoidDelayActivitiesRule());

        var finding = Assert.Single(findings);
        Assert.Equal("RPA001", finding.RuleId);
        Assert.Equal("00:00:04", finding.CurrentValue);
    }

    [Fact]
    public void Rpa001_ReturnsNoFinding_WhenDelayDoesNotExist()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("<Sequence />"));

        var findings = AnalyzeRule(project, new AvoidDelayActivitiesRule());

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa002_ReturnsFinding_ForEmptyCatch()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml(TryCatchWithCatchBody("")));

        var findings = AnalyzeRule(project, new EmptyCatchBlockRule());

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa002_ReturnsNoFinding_WhenCatchHasActivity()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml(TryCatchWithCatchBody("<ui:LogMessage />")));

        var findings = AnalyzeRule(project, new EmptyCatchBlockRule());

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa003_ReturnsFinding_ForSilentCatch()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml(TryCatchWithCatchBody("<Assign />")));

        var findings = AnalyzeRule(project, new ExceptionSilentlySwallowedRule());

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa003_ReturnsNoFinding_WhenCatchLogs()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml(TryCatchWithCatchBody("<ui:LogMessage />")));

        var findings = AnalyzeRule(project, new ExceptionSilentlySwallowedRule());

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa003_ReturnsNoFinding_WhenCatchRethrows()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml(TryCatchWithCatchBody("<Rethrow />")));

        var findings = AnalyzeRule(project, new ExceptionSilentlySwallowedRule());

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa004_ReturnsFinding_ForFiveSecondDelay()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <Delay Duration="00:00:05" />
            </Sequence.Activities>
          </Sequence>
        """));

        var findings = AnalyzeRule(project, new LongHardCodedDelayRule());

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa004_ReturnsNoFinding_ForFourSecondDelay()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <Delay Duration="00:00:04" />
            </Sequence.Activities>
          </Sequence>
        """));

        var findings = AnalyzeRule(project, new LongHardCodedDelayRule());

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa005_ReturnsFinding_ForMissingInvokeWorkflow()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:InvokeWorkflowFile WorkflowFileName="Framework\Missing.xaml" />
            </Sequence.Activities>
          </Sequence>
        """));

        var findings = AnalyzeRule(project, new InvalidInvokeWorkflowReferenceRule());

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa005_ReturnsNoFinding_ForExistingInvokeWorkflow()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:InvokeWorkflowFile WorkflowFileName="Framework\Existing.xaml" />
            </Sequence.Activities>
          </Sequence>
        """));
        project.WriteWorkflow("Framework/Existing.xaml", AnalysisTestProject.WorkflowXaml("<Sequence />"));

        var findings = AnalyzeRule(project, new InvalidInvokeWorkflowReferenceRule());

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa005_NormalizesWindowsAndUnixSeparators()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:InvokeWorkflowFile WorkflowFileName="Framework\Existing.xaml" />
            </Sequence.Activities>
          </Sequence>
        """));
        project.WriteWorkflow("Framework/Existing.xaml", AnalysisTestProject.WorkflowXaml("<Sequence />"));

        var findings = AnalyzeRule(project, new InvalidInvokeWorkflowReferenceRule());

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa005_NormalizesUnicodePathForms()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:InvokeWorkflowFile WorkflowFileName="IntegrationPoints\Web\Intervision\İslerimControl.xaml" />
            </Sequence.Activities>
          </Sequence>
        """));
        project.WriteWorkflow("IntegrationPoints/Web/Intervision/I\u0307slerimControl.xaml", AnalysisTestProject.WorkflowXaml("<Sequence />"));

        var findings = AnalyzeRule(project, new InvalidInvokeWorkflowReferenceRule());

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa006_ReturnsFinding_ForWorkflow1()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Workflow1.xaml", AnalysisTestProject.WorkflowXaml("<Sequence />"));

        var findings = AnalyzeRule(project, new WorkflowNamingConventionRule());

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa006_ReturnsNoFinding_ForProcessInvoice()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("ProcessInvoice.xaml", AnalysisTestProject.WorkflowXaml("<Sequence />"));

        var findings = AnalyzeRule(project, new WorkflowNamingConventionRule());

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa007_ReturnsFinding_ForGenericClickDisplayName()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click DisplayName="Click" />
            </Sequence.Activities>
          </Sequence>
        """));

        var findings = AnalyzeRule(project, new GenericActivityDisplayNameRule());

        var finding = Assert.Single(findings);
        Assert.Equal(UiPathFindingScope.Aggregated, finding.Scope);
        Assert.Equal(1, finding.AffectedActivityCount);
        Assert.Equal(1, finding.OccurrenceCount);
        Assert.Equal("Click", Assert.Single(finding.AffectedActivities).ActivityDisplayName);
    }

    [Fact]
    public void Rpa007_ReturnsNoFinding_ForSpecificClickDisplayName()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click DisplayName="Click Login Button" />
            </Sequence.Activities>
          </Sequence>
        """));

        var findings = AnalyzeRule(project, new GenericActivityDisplayNameRule());

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa007_AggregatesFiftyGenericDisplayNamesIntoOneWorkflowFinding()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml(GenericDisplayNameActivities(50)));

        var finding = Assert.Single(AnalyzeRule(project, new GenericActivityDisplayNameRule()));

        Assert.Equal(UiPathFindingScope.Aggregated, finding.Scope);
        Assert.Equal(50, finding.AffectedActivityCount);
        Assert.Equal(50, finding.OccurrenceCount);
        Assert.Equal(50, finding.AffectedActivities.Count);
        Assert.Equal(5, finding.ExampleActivities.Count);
        Assert.Equal(50, finding.TotalRelevantActivityCount);
        Assert.Equal(100, finding.Percentage);
    }

    [Fact]
    public void Rpa007_AggregatesPerWorkflow()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("First.xaml", AnalysisTestProject.WorkflowXaml(GenericDisplayNameActivities(2)));
        project.WriteWorkflow("Second.xaml", AnalysisTestProject.WorkflowXaml(GenericDisplayNameActivities(3)));

        var findings = AnalyzeRule(project, new GenericActivityDisplayNameRule());

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, finding => finding.WorkflowPath == "First.xaml" && finding.AffectedActivityCount == 2);
        Assert.Contains(findings, finding => finding.WorkflowPath == "Second.xaml" && finding.AffectedActivityCount == 3);
    }

    [Fact]
    public void Rpa007_AggregatedFindingKeepsAffectedActivityDetails()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click DisplayName="Click" sap2010:WorkflowViewState.IdRef="Click_1" />
              <ui:Click DisplayName="Click Login Button" sap2010:WorkflowViewState.IdRef="Click_2" />
            </Sequence.Activities>
          </Sequence>
        """));

        var finding = Assert.Single(AnalyzeRule(project, new GenericActivityDisplayNameRule()));

        var affected = Assert.Single(finding.AffectedActivities);
        Assert.Equal("Click_1", affected.ActivityId);
        Assert.Equal("Click_1", affected.StableId);
        Assert.Equal("Click", affected.ActivityName);
        Assert.Equal("DisplayName", affected.PropertyName);
    }

    [Fact]
    public void Rpa008_ReturnsFinding_ForLargeWorkflowWithoutLogging()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml(LargeWorkflow(includeLog: false)));

        var findings = AnalyzeRule(project, new WorkflowHasNoLoggingRule());

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa008_ReturnsNoFinding_ForLargeWorkflowWithLogging()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml(LargeWorkflow(includeLog: true)));

        var findings = AnalyzeRule(project, new WorkflowHasNoLoggingRule());

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa008_ReturnsNoFinding_ForSmallWorkflowWithoutLogging()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <Assign />
              <ui:Click />
            </Sequence.Activities>
          </Sequence>
        """));

        var findings = AnalyzeRule(project, new WorkflowHasNoLoggingRule());

        Assert.Empty(findings);
    }

    [Fact]
    public void RuleEngine_Continues_WhenOneRuleFails()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <Delay Duration="00:00:01" />
            </Sequence.Activities>
          </Sequence>
        """));
        var context = CreateContext(project);
        var engine = new UiPathRuleEngine([new ThrowingRule(), new AvoidDelayActivitiesRule()]);

        var result = engine.Analyze(context);

        Assert.Equal(2, result.TotalFindings);
        Assert.Contains(result.Findings, finding => finding.RuleId == "FAIL001");
        Assert.Contains(result.Findings, finding => finding.RuleId == "RPA001");
    }

    [Fact]
    public void RuleEngine_SkipsDisabledRules_WhenProfileIsProvided()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <Delay Duration="00:00:01" />
            </Sequence.Activities>
          </Sequence>
        """));
        var profile = Profile(new UiPathRuleConfiguration
        {
            RuleId = "RPA001",
            Enabled = false,
            Weight = 2,
            MaxPenalty = 10
        });
        var engine = new UiPathRuleEngine([new AvoidDelayActivitiesRule()]);

        var result = engine.Analyze(CreateContext(project), profile);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void RuleEngine_AppliesSeverityOverrideToFindings()
    {
        using var project = AnalysisTestProject.Create();
        project.WriteWorkflow("Main.xaml", AnalysisTestProject.WorkflowXaml("""
          <Sequence>
            <Sequence.Activities>
              <Delay Duration="00:00:01" />
            </Sequence.Activities>
          </Sequence>
        """));
        var profile = Profile(new UiPathRuleConfiguration
        {
            RuleId = "RPA001",
            Enabled = true,
            SeverityOverride = RuleSeverity.Suggestion,
            Weight = 2,
            MaxPenalty = 10
        });
        var engine = new UiPathRuleEngine([new AvoidDelayActivitiesRule()]);

        var result = engine.Analyze(CreateContext(project), profile);

        Assert.Equal(RuleSeverity.Suggestion, Assert.Single(result.Findings).Severity);
    }

    [Fact]
    public void StaticAnalysisResult_ReturnsSeverityCounts()
    {
        var result = new UiPathStaticAnalysisResult();
        result.Findings.Add(new UiPathAnalysisFinding
        {
            RuleId = "A",
            RuleName = "A",
            Severity = RuleSeverity.Error,
            Category = RuleCategory.Reliability,
            Message = "A"
        });
        result.Findings.Add(new UiPathAnalysisFinding
        {
            RuleId = "B",
            RuleName = "B",
            Severity = RuleSeverity.Warning,
            Category = RuleCategory.Reliability,
            Message = "B"
        });
        result.Findings.Add(new UiPathAnalysisFinding
        {
            RuleId = "C",
            RuleName = "C",
            Severity = RuleSeverity.Suggestion,
            Category = RuleCategory.Reliability,
            Message = "C"
        });

        Assert.Equal(3, result.TotalFindings);
        Assert.Equal(3, result.TotalOccurrences);
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(1, result.WarningCount);
        Assert.Equal(1, result.SuggestionCount);
        Assert.Equal(0, result.CriticalCount);
        Assert.Equal(0, result.InfoCount);
    }

    [Fact]
    public void StaticAnalysisResult_CountsAggregatedOccurrencesSeparately()
    {
        var result = new UiPathStaticAnalysisResult();
        result.Findings.Add(new UiPathAnalysisFinding
        {
            RuleId = "RPA007",
            RuleName = "Generic Activity Display Name",
            Severity = RuleSeverity.Suggestion,
            Category = RuleCategory.Maintainability,
            Message = "50 generic display names.",
            Scope = UiPathFindingScope.Aggregated,
            OccurrenceCount = 50
        });

        Assert.Equal(1, result.TotalFindings);
        Assert.Equal(50, result.TotalOccurrences);
    }

    private static IReadOnlyList<UiPathAnalysisFinding> AnalyzeRule(AnalysisTestProject project, IUiPathAnalysisRule rule)
    {
        return rule.Analyze(CreateContext(project)).ToArray();
    }

    private static UiPathAnalysisContext CreateContext(AnalysisTestProject project)
    {
        return new UiPathAnalysisContext
        {
            Project = new UiPathProjectScanner().Scan(project.RootPath)
        };
    }

    private static UiPathRuleProfile Profile(params UiPathRuleConfiguration[] configurations)
    {
        return new UiPathRuleProfile
        {
            Id = "test",
            Name = "Test",
            Rules = configurations
        };
    }

    private static string TryCatchWithCatchBody(string catchBody)
    {
        return $$"""
          <TryCatch>
            <TryCatch.Try>
              <Sequence />
            </TryCatch.Try>
            <TryCatch.Catches>
              <Catch x:TypeArguments="s:Exception">
                <ActivityAction x:TypeArguments="s:Exception">
                  <ActivityAction.Argument>
                    <DelegateInArgument x:TypeArguments="s:Exception" Name="exception" />
                  </ActivityAction.Argument>
                  <Sequence>
                    <Sequence.Activities>
                      {{catchBody}}
                    </Sequence.Activities>
                  </Sequence>
                </ActivityAction>
              </Catch>
            </TryCatch.Catches>
          </TryCatch>
        """;
    }

    private static string LargeWorkflow(bool includeLog)
    {
        var maybeLogMessage = includeLog ? "<ui:LogMessage />" : "<Assign />";
        return $$"""
          <Sequence>
            <Sequence.Activities>
              <Assign />
              <Assign />
              <ui:Click />
              <ui:TypeInto />
              <Delay Duration="00:00:01" />
              {{maybeLogMessage}}
            </Sequence.Activities>
          </Sequence>
        """;
    }

    private static string GenericDisplayNameActivities(int count)
    {
        var activities = string.Join(Environment.NewLine, Enumerable.Range(0, count)
            .Select(index => $"""<ui:Click DisplayName="Click" sap2010:WorkflowViewState.IdRef="Click_{index}" />"""));
        return $$"""
          <Sequence>
            <Sequence.Activities>
              {{activities}}
            </Sequence.Activities>
          </Sequence>
        """;
    }

    private sealed class ThrowingRule : IUiPathAnalysisRule
    {
        public string Id => "FAIL001";

        public string Name => "Throwing Rule";

        public string Description => "Test rule that throws.";

        public RuleSeverity Severity => RuleSeverity.Warning;

        public RuleCategory Category => RuleCategory.Reliability;

        public IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
        {
            throw new InvalidOperationException("Intentional test failure.");
        }
    }

    private sealed class AnalysisTestProject : IDisposable
    {
        public string RootPath { get; } = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantAnalysisTests-{Guid.NewGuid():N}");

        private AnalysisTestProject()
        {
            Directory.CreateDirectory(RootPath);
            File.WriteAllText(Path.Combine(RootPath, "project.json"), """
            {
              "name": "AnalysisTestProject",
              "dependencies": {}
            }
            """);
        }

        public static AnalysisTestProject Create()
        {
            return new AnalysisTestProject();
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
              xmlns:sap2010="http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation"
              xmlns:ui="http://schemas.uipath.com/workflow/activities"
              xmlns:s="clr-namespace:System;assembly=System.Private.CoreLib"
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
}

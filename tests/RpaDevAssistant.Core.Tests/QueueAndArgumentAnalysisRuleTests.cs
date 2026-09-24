using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.RuleCatalog;
using RpaDevAssistant.Core.Analysis.Rules;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Localization;
using RpaDevAssistant.Core.Models;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class QueueAndArgumentAnalysisRuleTests
{
    private readonly HardCodedQueueNameRule queueRule = new(new UiPathExpressionClassifier());
    private readonly InvalidArgumentDefaultValueRule defaultRule = new();

    [Fact]
    public void Rpa047_ReportsLiteralQueueNameOnKnownQueueActivity()
    {
        var finding = Assert.Single(queueRule.Analyze(Context(Workflow(Activity(
            "AddQueueItem",
            ("QueueName", "[\"FinanceQueue\"]"))))));

        Assert.Equal("RPA047", finding.RuleId);
        Assert.Equal("FinanceQueue", finding.CurrentValue);
        Assert.Equal(RuleCategory.Orchestrator, finding.Category);
    }

    [Theory]
    [InlineData("Config(\"QueueName\")")]
    [InlineData("in_QueueName")]
    [InlineData("GetQueueName()")]
    public void Rpa047_IgnoresConfiguredOrDynamicQueueName(string queueName)
    {
        var findings = queueRule.Analyze(Context(Workflow(Activity(
            "GetTransactionItem",
            ("QueueName", queueName)))));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa047_IgnoresQueueNamePropertyOnUnrelatedActivity()
    {
        var findings = queueRule.Analyze(Context(Workflow(Activity(
            "Assign",
            ("QueueName", "[\"FinanceQueue\"]")))));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa048_ReportsDefaultOnOutArgument()
    {
        var workflow = Workflow();
        workflow.Arguments.Add(Argument("out_Result", "OutArgument(x:String)", "[\"unused\"]"));

        var finding = Assert.Single(defaultRule.Analyze(Context(workflow)));

        Assert.Equal("RPA048", finding.RuleId);
        Assert.Equal("ArgumentDefaultValue", finding.PropertyName);
        Assert.Equal(UiPathFindingScope.Workflow, finding.Scope);
    }

    [Theory]
    [InlineData("InArgument(x:Int32)")]
    [InlineData("InOutArgument(System.Guid)")]
    public void Rpa048_ReportsNullDefaultForNonNullableValueType(string type)
    {
        var workflow = Workflow();
        workflow.Arguments.Add(Argument("in_Value", type, "{x:Null}"));

        Assert.Single(defaultRule.Analyze(Context(workflow)));
    }

    [Fact]
    public void Rpa048_AllowsIntentionalInputDefaultsAndNullableNull()
    {
        var workflow = Workflow();
        workflow.Arguments.Add(Argument("in_Count", "InArgument(x:Int32)", "[3]"));
        workflow.Arguments.Add(Argument("in_Name", "InArgument(x:String)", "{x:Null}"));
        workflow.Arguments.Add(Argument("in_Optional", "InArgument(System.Nullable(x:Int32))", "Nothing"));
        workflow.Arguments.Add(new UiPathArgumentInfo
        {
            Name = "in_Required",
            Direction = "In",
            Type = "InArgument(x:String)",
            HasDefaultValue = false
        });

        Assert.Empty(defaultRule.Analyze(Context(workflow)));
    }

    [Fact]
    public void DefaultProfileAndLocalizationRegisterNewRules()
    {
        var profile = new BuiltInUiPathRuleProfileProvider().GetProfile("default");
        var localizer = new RpaDevAssistantLocalizer();

        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA047" && rule.Enabled && rule.Weight == 3);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA048" && rule.Enabled && rule.Weight == 2);
        Assert.Equal("Hard-Coded Queue Name", localizer.Get("Rules.RPA047.Name", "en"));
        Assert.Equal("Hard-Coded Queue Adı", localizer.Get("Rules.RPA047.Name", "tr"));
        Assert.Equal("Invalid Argument Default Value", localizer.Get("Rules.RPA048.Name", "en"));
        Assert.Equal("Geçersiz Argument Default Value", localizer.Get("Rules.RPA048.Name", "tr"));
    }

    [Fact]
    public void RuleCatalogExposesQueueAndArgumentDefaultRulesWithCorrectScope()
    {
        var provider = new UiPathRuleCatalogProvider(
            [queueRule, defaultRule],
            [],
            new UiPathMutationPolicy(),
            new RpaDevAssistantLocalizer());

        var rules = provider.GetRules("tr");

        var queue = Assert.Single(rules, rule => rule.Id == "RPA047");
        Assert.Equal(UiPathRuleScope.Activity, queue.Scope);
        Assert.Equal(RuleCategory.Orchestrator, queue.Category);
        Assert.Equal("Hard-Coded Queue Adı", queue.Name);

        var argumentDefault = Assert.Single(rules, rule => rule.Id == "RPA048");
        Assert.Equal(UiPathRuleScope.Workflow, argumentDefault.Scope);
        Assert.Equal("Geçersiz Argument Default Value", argumentDefault.Name);
    }

    private static UiPathArgumentInfo Argument(string name, string type, string defaultValue) => new()
    {
        Name = name,
        Direction = type.StartsWith("InOut", StringComparison.Ordinal) ? "InOut"
            : type.StartsWith("Out", StringComparison.Ordinal) ? "Out" : "In",
        Type = type,
        DefaultValue = defaultValue,
        HasDefaultValue = true
    };

    private static UiPathActivityInfo Activity(string name, params (string Name, string? Value)[] properties) => new()
    {
        ActivityId = Guid.NewGuid().ToString("N"),
        Name = name,
        DisplayName = name,
        TypeName = name,
        XamlFile = "Main.xaml",
        Properties = properties.ToDictionary(property => property.Name, property => property.Value)
    };

    private static UiPathWorkflowAnalysis Workflow(params UiPathActivityInfo[] activities)
    {
        var workflow = new UiPathWorkflowAnalysis { FileName = "Main.xaml", RelativePath = "Main.xaml" };
        workflow.Activities.AddRange(activities);
        return workflow;
    }

    private static UiPathAnalysisContext Context(params UiPathWorkflowAnalysis[] workflows)
    {
        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/project",
            ProjectName = "TestProject",
            ProjectFolderExists = true,
            ProjectJsonExists = true,
            ProjectJsonParsed = true
        };
        foreach (var workflow in workflows)
        {
            project.Workflows.Add(new UiPathWorkflowInfo
            {
                Name = workflow.FileName,
                RelativePath = workflow.RelativePath,
                FullPath = Path.Combine(project.ProjectPath, workflow.RelativePath),
                Analysis = workflow
            });
        }

        return new UiPathAnalysisContext { Project = project };
    }
}

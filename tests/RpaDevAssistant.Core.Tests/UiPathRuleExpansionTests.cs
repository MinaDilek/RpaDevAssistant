using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Rules;
using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Models;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathRuleExpansionTests
{
    private readonly UiPathExpressionClassifier expressionClassifier = new();
    private readonly UiPathSelectorAnalyzer selectorAnalyzer = new();
    private readonly UiPathWorkflowMetricsCalculator metricsCalculator = new();

    [Fact]
    public void Rpa009_ReturnsFinding_ForPasswordLiteral()
    {
        var findings = Analyze(new HardCodedCredentialLikeValueRule(expressionClassifier), Activity("Assign", properties: new Dictionary<string, string?>
        {
            ["Password"] = "\"Test123!\""
        }));

        var finding = Assert.Single(findings);
        Assert.Equal("RPA009", finding.RuleId);
        Assert.Equal("[REDACTED]", finding.CurrentValue);
    }

    [Theory]
    [InlineData("Config(\"Password\")")]
    [InlineData("in_Token")]
    public void Rpa009_ReturnsNoFinding_ForConfigOrVariableReference(string value)
    {
        var findings = Analyze(new HardCodedCredentialLikeValueRule(expressionClassifier), Activity("Assign", properties: new Dictionary<string, string?>
        {
            ["Password"] = value
        }));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa009_ReturnsNoFinding_ForNormalPropertyContainingPasswordWord()
    {
        var findings = Analyze(new HardCodedCredentialLikeValueRule(expressionClassifier), Activity("Assign", properties: new Dictionary<string, string?>
        {
            ["Message"] = "Password loaded successfully"
        }));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa009_ReturnsNoFinding_ForXamlNullPasswordMarker()
    {
        var findings = Analyze(new HardCodedCredentialLikeValueRule(expressionClassifier), Activity("SendMail", properties: new Dictionary<string, string?>
        {
            ["Password"] = "{x:Null}"
        }));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa010_ReturnsFinding_WhenLogConcatenatesPasswordVariable()
    {
        var findings = Analyze(new SensitiveValueInLogMessageRule(), Activity("LogMessage", properties: new Dictionary<string, string?>
        {
            ["Message"] = "\"Password: \" + password"
        }));

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa010_ReturnsNoFinding_ForSafeStatusLiteral()
    {
        var findings = Analyze(new SensitiveValueInLogMessageRule(), Activity("LogMessage", properties: new Dictionary<string, string?>
        {
            ["Message"] = "Password loaded successfully"
        }));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa010_ReturnsFinding_WhenLogReferencesTokenValue()
    {
        var findings = Analyze(new SensitiveValueInLogMessageRule(), Activity("LogMessage", properties: new Dictionary<string, string?>
        {
            ["Message"] = "\"token=\" + accessToken"
        }));

        Assert.Single(findings);
    }

    [Theory]
    [InlineData("120000", true)]
    [InlineData("60000", false)]
    [InlineData("Config(\"Timeout\")", false)]
    public void Rpa011_DetectsExcessiveTimeoutOnlyForLiteralValues(string timeout, bool expectedFinding)
    {
        var findings = Analyze(new ExcessiveUiTimeoutRule(expressionClassifier), Activity("Click", properties: new Dictionary<string, string?>
        {
            ["TimeoutMS"] = timeout
        }));

        Assert.Equal(expectedFinding, findings.Count == 1);
    }

    [Theory]
    [InlineData("500", true)]
    [InlineData("3000", false)]
    [InlineData("0", false)]
    public void Rpa012_DetectsVeryLowTimeoutButSkipsZeroAmbiguity(string timeout, bool expectedFinding)
    {
        var findings = Analyze(new UnrealisticallyLowUiTimeoutRule(expressionClassifier), Activity("Click", properties: new Dictionary<string, string?>
        {
            ["TimeoutMS"] = timeout
        }));

        Assert.Equal(expectedFinding, findings.Count == 1);
    }

    [Theory]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void Rpa013_DetectsContinueOnErrorTrue(string value, bool expectedFinding)
    {
        var findings = Analyze(new ContinueOnErrorEnabledRule(), Activity("Assign", properties: new Dictionary<string, string?>
        {
            ["ContinueOnError"] = value
        }));

        Assert.Equal(expectedFinding, findings.Count == 1);
    }

    [Fact]
    public void Rpa014_ReturnsWorkflowFinding_WhenThresholdReached()
    {
        var activities = Enumerable.Range(0, 3)
            .Select(index => Activity("Assign", id: $"a{index}", properties: new Dictionary<string, string?>
            {
                ["ContinueOnError"] = "True"
            }))
            .ToArray();

        var findings = Analyze(new ExcessiveContinueOnErrorUsageRule(metricsCalculator), activities);

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa014_ReturnsNoFinding_BelowThreshold()
    {
        var findings = Analyze(
            new ExcessiveContinueOnErrorUsageRule(metricsCalculator),
            Activity("Assign", id: "a1", properties: new Dictionary<string, string?> { ["ContinueOnError"] = "True" }),
            Activity("Assign", id: "a2", properties: new Dictionary<string, string?> { ["ContinueOnError"] = "True" }));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa030_DetectsBusinessRuleExceptionCatchWithoutVisibleHandling()
    {
        var findings = Analyze(
            new BusinessRuleExceptionHandlingRule(),
            Activity("Catch", id: "catch", properties: new Dictionary<string, string?> { ["TypeArguments"] = "ui:BusinessRuleException" }),
            Activity("Sequence", id: "body", parentId: "catch"),
            Activity("Assign", id: "assign", parentId: "body"));

        var finding = Assert.Single(findings);
        Assert.Equal("RPA030", finding.RuleId);
        Assert.Equal("ui:BusinessRuleException", finding.CurrentValue);
    }

    [Fact]
    public void Rpa030_IgnoresGeneralExceptionCatch()
    {
        var findings = Analyze(
            new BusinessRuleExceptionHandlingRule(),
            Activity("Catch", id: "catch", properties: new Dictionary<string, string?> { ["TypeArguments"] = "s:Exception" }),
            Activity("Sequence", id: "body", parentId: "catch"),
            Activity("Assign", id: "assign", parentId: "body"));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa030_IgnoresBusinessRuleExceptionCatchWithLogMessage()
    {
        var findings = Analyze(
            new BusinessRuleExceptionHandlingRule(),
            Activity("Catch", id: "catch", properties: new Dictionary<string, string?> { ["TypeArguments"] = "ui:BusinessRuleException" }),
            Activity("Sequence", id: "body", parentId: "catch"),
            Activity("LogMessage", id: "log", parentId: "body"));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa030_IgnoresBusinessRuleExceptionCatchWithTransactionStatusWorkflow()
    {
        var findings = Analyze(
            new BusinessRuleExceptionHandlingRule(),
            Activity("Catch", id: "catch", properties: new Dictionary<string, string?> { ["TypeArguments"] = "ui:BusinessRuleException" }),
            Activity("Sequence", id: "body", parentId: "catch"),
            Activity("InvokeWorkflowFile", id: "invoke", parentId: "body", properties: new Dictionary<string, string?>
            {
                ["WorkflowFileName"] = "Framework/SetTransactionStatus.xaml"
            }));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa017_DetectsIdxSelector()
    {
        var findings = Analyze(new SelectorUsesIdxAttributeRule(selectorAnalyzer), Activity("Click", properties: new Dictionary<string, string?>
        {
            ["Selector"] = "<webctrl tag='BUTTON' idx='3' />"
        }));

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa017_IgnoresStableSelector()
    {
        var findings = Analyze(new SelectorUsesIdxAttributeRule(selectorAnalyzer), Activity("Click", properties: new Dictionary<string, string?>
        {
            ["Selector"] = "<webctrl tag='BUTTON' aaname='Login' />"
        }));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa018_DetectsHighConfidenceGeneratedId()
    {
        var findings = Analyze(new PotentiallyUnstableSelectorAttributeRule(selectorAnalyzer), Activity("Click", properties: new Dictionary<string, string?>
        {
            ["Selector"] = "<webctrl tag='DIV' id='123456789' />"
        }));

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa018_IgnoresNormalStableId()
    {
        var findings = Analyze(new PotentiallyUnstableSelectorAttributeRule(selectorAnalyzer), Activity("Click", properties: new Dictionary<string, string?>
        {
            ["Selector"] = "<webctrl tag='DIV' id='login-panel' />"
        }));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa019_DetectsLongSelector()
    {
        var findings = Analyze(new OverlyComplexSelectorRule(selectorAnalyzer), Activity("Click", properties: new Dictionary<string, string?>
        {
            ["Selector"] = $"<webctrl aaname='{new string('a', 510)}' />"
        }));

        Assert.Single(findings);
    }

    [Fact]
    public void SelectorAnalyzer_DoesNotThrow_ForInvalidSelector()
    {
        var analysis = selectorAnalyzer.Analyze("<webctrl tag='DIV'");

        Assert.False(analysis.ContainsIdx);
    }

    [Theory]
    [InlineData("\"C:\\Temp\\file.xlsx\"", true)]
    [InlineData("Config(\"OutputFolder\")", false)]
    [InlineData("Path.Combine(Config(\"OutputFolder\"), \"file.xlsx\")", false)]
    [InlineData("Data\\file.xlsx", false)]
    public void Rpa020_DetectsHardCodedAbsolutePaths(string value, bool expectedFinding)
    {
        var findings = Analyze(new HardCodedAbsoluteFilePathRule(expressionClassifier), Activity("Assign", properties: new Dictionary<string, string?>
        {
            ["FilePath"] = value
        }));

        Assert.Equal(expectedFinding, findings.Count == 1);
    }

    [Fact]
    public void Rpa021_DetectsEmailOnlyInMailActivity()
    {
        var mailFindings = Analyze(new HardCodedEmailAddressRule(expressionClassifier), Activity("SendMail", properties: new Dictionary<string, string?>
        {
            ["To"] = "user@company.com"
        }));
        var nonMailFindings = Analyze(new HardCodedEmailAddressRule(expressionClassifier), Activity("Assign", properties: new Dictionary<string, string?>
        {
            ["Text"] = "user@company.com"
        }));

        Assert.Single(mailFindings);
        Assert.Empty(nonMailFindings);
    }

    [Fact]
    public void Rpa022_DetectsHttpRequestWithoutTimeout()
    {
        var findings = Analyze(new HttpRequestWithoutExplicitTimeoutRule(expressionClassifier), Activity("HttpRequest"));

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa022_IgnoresHttpRequestWithValidTimeout()
    {
        var findings = Analyze(new HttpRequestWithoutExplicitTimeoutRule(expressionClassifier), Activity("HttpRequest", properties: new Dictionary<string, string?>
        {
            ["Timeout"] = "30000"
        }));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa022_IgnoresHttpRequestWithConfiguredTimeout()
    {
        var findings = Analyze(new HttpRequestWithoutExplicitTimeoutRule(expressionClassifier), Activity("HttpRequest", properties: new Dictionary<string, string?>
        {
            ["Timeout"] = "Config(\"HttpTimeout\")"
        }));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa023_DetectsHttpRequestOutsideTryCatch()
    {
        var findings = Analyze(new HttpRequestWithoutLocalErrorHandlingRule(), Activity("HttpRequest"));

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa023_IgnoresNestedHttpRequestInsideTryCatch()
    {
        var findings = Analyze(
            new HttpRequestWithoutLocalErrorHandlingRule(),
            Activity("TryCatch", id: "try"),
            Activity("Sequence", id: "body", parentId: "try"),
            Activity("HttpRequest", id: "http", parentId: "body"));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa024_DetectsWorkflowWithTooManyArguments()
    {
        var workflow = Workflow(Activity("Assign"));
        for (var index = 0; index < 11; index++)
        {
            workflow.Arguments.Add(new UiPathArgumentInfo { Name = $"arg{index}" });
        }

        var findings = new ExcessiveWorkflowArgumentsRule(metricsCalculator)
            .Analyze(Context(workflow))
            .ToArray();

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa024_IgnoresWorkflowAtArgumentThreshold()
    {
        var workflow = Workflow(Activity("Assign"));
        for (var index = 0; index < 10; index++)
        {
            workflow.Arguments.Add(new UiPathArgumentInfo { Name = $"arg{index}" });
        }

        var findings = new ExcessiveWorkflowArgumentsRule(metricsCalculator)
            .Analyze(Context(workflow))
            .ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa025_DetectsLargeWorkflow()
    {
        var activities = Enumerable.Range(0, 101)
            .Select(index => Activity("Assign", id: $"a{index}"))
            .ToArray();

        var findings = Analyze(new LargeWorkflowRule(metricsCalculator), activities);

        Assert.Single(findings);
    }

    [Fact]
    public void Rpa025_IgnoresModerateWorkflow()
    {
        var activities = Enumerable.Range(0, 50)
            .Select(index => Activity("Assign", id: $"a{index}"))
            .ToArray();

        var findings = Analyze(new LargeWorkflowRule(metricsCalculator), activities);

        Assert.Empty(findings);
    }

    [Fact]
    public void WorkflowMetrics_CalculatesComplexityInputs()
    {
        var workflow = Workflow(
            Activity("Sequence", id: "root", parentId: null) with { Depth = 0 },
            Activity("If", id: "if", parentId: "root") with { Depth = 1 },
            Activity("Switch", id: "switch", parentId: "if") with { Depth = 2 },
            Activity("While", id: "loop", parentId: "switch") with { Depth = 3 },
            Activity("TryCatch", id: "try", parentId: "loop") with { Depth = 4 },
            Activity("Assign", id: "assign", parentId: "try") with { Depth = 5 });
        workflow.Arguments.Add(new UiPathArgumentInfo { Name = "in_Item" });

        var metrics = metricsCalculator.Calculate(workflow);

        Assert.Equal(6, metrics.TotalActivities);
        Assert.Equal(1, metrics.ExecutableActivityCount);
        Assert.Equal(5, metrics.ContainerActivityCount);
        Assert.Equal(5, metrics.MaxNestingDepth);
        Assert.Equal(1, metrics.IfCount);
        Assert.Equal(1, metrics.SwitchCount);
        Assert.Equal(1, metrics.LoopCount);
        Assert.Equal(1, metrics.TryCatchCount);
        Assert.Equal(1, metrics.ArgumentCount);
        Assert.True(metrics.ComplexityScore > metrics.ExecutableActivityCount);
    }

    [Theory]
    [InlineData(10, UiPathWorkflowComplexityLevel.Low)]
    [InlineData(45, UiPathWorkflowComplexityLevel.Medium)]
    [InlineData(95, UiPathWorkflowComplexityLevel.High)]
    [InlineData(170, UiPathWorkflowComplexityLevel.VeryHigh)]
    public void WorkflowMetrics_MapsComplexityLevelThresholds(int score, UiPathWorkflowComplexityLevel expected)
    {
        Assert.Equal(expected, UiPathWorkflowMetricsCalculator.CalculateComplexityLevel(score));
    }

    [Fact]
    public void WorkflowMetrics_DoesNotOverPenalizeInvokeHeavyFlatWorkflow()
    {
        var activities = Enumerable.Range(0, 20)
            .Select(index => Activity("InvokeWorkflowFile", id: $"invoke{index}") with { Depth = 1 })
            .ToArray();

        var metrics = metricsCalculator.Calculate(Workflow(activities));

        Assert.Equal(20, metrics.InvokeWorkflowCount);
        Assert.Equal(UiPathWorkflowComplexityLevel.Low, metrics.ComplexityLevel);
    }

    [Fact]
    public void Rpa025_DetectsWorkflowByComplexityEvenBelowActivityThreshold()
    {
        var activities = new List<UiPathActivityInfo>();
        for (var index = 0; index < 25; index++)
        {
            activities.Add(Activity("If", id: $"if{index}") with { Depth = 10 });
        }

        var findings = Analyze(new LargeWorkflowRule(metricsCalculator), activities.ToArray());

        var finding = Assert.Single(findings);
        Assert.Contains("complexity", finding.CurrentValue);
    }

    [Fact]
    public void RuleCatalog_ReturnsNewRulesWithFixAndAutoApplyMetadata()
    {
        var provider = new UiPathRuleCatalogProvider(
            [new GenericActivityDisplayNameRule(), new SelectorUsesIdxAttributeRule(selectorAnalyzer)],
            [new StaticFixProvider(["RPA007", "RPA017"])],
            new UiPathMutationPolicy());

        var rules = provider.GetRules();

        Assert.Contains(rules, rule => rule.Id == "RPA017" && rule.HasFixSuggestion && !rule.CanAutoApply);
        Assert.Contains(rules, rule => rule.Id == "RPA007" && rule.HasFixSuggestion && rule.CanAutoApply);
    }

    [Fact]
    public void DefaultProfile_IncludesExpandedRuleSet()
    {
        var profile = new BuiltInUiPathRuleProfileProvider().GetProfile("default");

        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA009" && rule.Weight == 15 && rule.MaxPenalty == 30);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA025" && rule.Weight == 5 && rule.MaxPenalty == 15);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA030" && rule.Enabled && rule.Weight == 5 && rule.MaxPenalty == 15);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA046" && rule.Enabled && rule.Weight == 1 && rule.MaxPenalty == 5);
    }

    [Fact]
    public void Rpa046_ReturnsOneWorkflowFinding_ForRepeatedLongFixedDelaysWithoutStateWait()
    {
        var findings = Analyze(
            new FixedDelaysWithoutStateBasedWaitRule(expressionClassifier),
            Activity("Delay", properties: new Dictionary<string, string?> { ["Duration"] = "00:00:05" }),
            Activity("Delay", properties: new Dictionary<string, string?> { ["Duration"] = "00:00:10" }),
            Activity("Click"));

        var finding = Assert.Single(findings);
        Assert.Equal("RPA046", finding.RuleId);
        Assert.Equal("Main.xaml", finding.WorkflowPath);
        Assert.Null(finding.ActivityId);
        Assert.Equal("fixedDelays=2; retryScopes=0; checkAppStates=0", finding.CurrentValue);
    }

    [Fact]
    public void Rpa046_ReturnsOneFindingPerAffectedWorkflow()
    {
        var first = Workflow(
            Activity("Delay", properties: new Dictionary<string, string?> { ["Duration"] = "00:00:05" }),
            Activity("Delay", properties: new Dictionary<string, string?> { ["Duration"] = "00:00:06" }),
            Activity("Click"));
        var second = new UiPathWorkflowAnalysis
        {
            FileName = "Login.xaml",
            RelativePath = "Business/Login.xaml"
        };
        second.Activities.AddRange(
        [
            Activity("Delay", properties: new Dictionary<string, string?> { ["Duration"] = "00:00:07" }),
            Activity("Delay", properties: new Dictionary<string, string?> { ["Duration"] = "00:00:08" }),
            Activity("TypeInto")
        ]);

        var findings = new FixedDelaysWithoutStateBasedWaitRule(expressionClassifier)
            .Analyze(Context(first, second))
            .ToArray();

        Assert.Equal(2, findings.Length);
        Assert.Contains(findings, finding => finding.WorkflowPath == "Main.xaml");
        Assert.Contains(findings, finding => finding.WorkflowPath == "Business/Login.xaml");
    }

    [Theory]
    [InlineData("00:00:04", "00:00:04")]
    [InlineData("Config(\"DelayDuration\")", "00:00:10")]
    public void Rpa046_IgnoresShortOrConfiguredDelayPairs(string firstDuration, string secondDuration)
    {
        var findings = Analyze(
            new FixedDelaysWithoutStateBasedWaitRule(expressionClassifier),
            Activity("Delay", properties: new Dictionary<string, string?> { ["Duration"] = firstDuration }),
            Activity("Delay", properties: new Dictionary<string, string?> { ["Duration"] = secondDuration }),
            Activity("Click"));

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("RetryScope")]
    [InlineData("Retry Scope")]
    [InlineData("CheckAppState")]
    [InlineData("Check App State")]
    public void Rpa046_ReturnsNoFinding_WhenStateBasedWaitExists(string stateWaitName)
    {
        var findings = Analyze(
            new FixedDelaysWithoutStateBasedWaitRule(expressionClassifier),
            Activity("Delay", properties: new Dictionary<string, string?> { ["Duration"] = "00:00:05" }),
            Activity("Delay", properties: new Dictionary<string, string?> { ["Duration"] = "00:00:06" }),
            Activity("Click"),
            Activity(stateWaitName));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa046_ReturnsNoFinding_WithoutUiActivity()
    {
        var findings = Analyze(
            new FixedDelaysWithoutStateBasedWaitRule(expressionClassifier),
            Activity("Delay", properties: new Dictionary<string, string?> { ["Duration"] = "00:00:05" }),
            Activity("Delay", properties: new Dictionary<string, string?> { ["Duration"] = "00:00:06" }),
            Activity("Assign"));

        Assert.Empty(findings);
    }

    [Fact]
    public void DefaultProfile_DisablesAmbiguousRpa015()
    {
        var profile = new BuiltInUiPathRuleProfileProvider().GetProfile("default");

        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA015" && !rule.Enabled);
    }

    [Fact]
    public void Rpa035_ReturnsFinding_ForHardCodedUrl()
    {
        var findings = Analyze(new HardCodedUrlRule(expressionClassifier), Activity("HttpClient", properties: new Dictionary<string, string?>
        {
            ["Endpoint"] = "\"https://api.example.com/v1/orders\""
        }));

        var finding = Assert.Single(findings);
        Assert.Equal("RPA035", finding.RuleId);
        Assert.Equal("https://api.example.com/v1/orders", finding.CurrentValue);
    }

    [Fact]
    public void Rpa035_ReturnsNoFinding_ForSchemaUrlOrConfig()
    {
        var schemaFindings = Analyze(new HardCodedUrlRule(expressionClassifier), Activity("Assign", properties: new Dictionary<string, string?>
        {
            ["Namespace"] = "http://schemas.microsoft.com/workflow"
        }));
        var configFindings = Analyze(new HardCodedUrlRule(expressionClassifier), Activity("HttpClient", properties: new Dictionary<string, string?>
        {
            ["Endpoint"] = "Config(\"OrderApiUrl\")"
        }));

        Assert.Empty(schemaFindings);
        Assert.Empty(configFindings);
    }

    [Fact]
    public void Rpa036_ReturnsFinding_ForCircularWorkflowReference()
    {
        var wfA = new UiPathWorkflowAnalysis { FileName = "A.xaml", RelativePath = "A.xaml" };
        wfA.Activities.Add(Activity("InvokeWorkflowFile", properties: new Dictionary<string, string?> { ["WorkflowFileName"] = "B.xaml" }));

        var wfB = new UiPathWorkflowAnalysis { FileName = "B.xaml", RelativePath = "B.xaml" };
        wfB.Activities.Add(Activity("InvokeWorkflowFile", properties: new Dictionary<string, string?> { ["WorkflowFileName"] = "A.xaml" }));

        var project = new ProjectScanResult { ProjectPath = "/tmp/project" };
        project.Workflows.Add(new UiPathWorkflowInfo { Name = "A.xaml", RelativePath = "A.xaml", FullPath = "/tmp/project/A.xaml", Analysis = wfA });
        project.Workflows.Add(new UiPathWorkflowInfo { Name = "B.xaml", RelativePath = "B.xaml", FullPath = "/tmp/project/B.xaml", Analysis = wfB });

        var rule = new CircularWorkflowReferenceRule();
        var findings = rule.Analyze(new UiPathAnalysisContext { Project = project }).ToList();

        Assert.NotEmpty(findings);
        Assert.Contains(findings, f => f.RuleId == "RPA036");
    }

    [Fact]
    public void Rpa037_ReturnsFinding_ForUnusedWorkflow()
    {
        var main = new UiPathWorkflowAnalysis { FileName = "Main.xaml", RelativePath = "Main.xaml" };
        main.Activities.Add(Activity("InvokeWorkflowFile", properties: new Dictionary<string, string?> { ["WorkflowFileName"] = "Process.xaml" }));

        var process = new UiPathWorkflowAnalysis { FileName = "Process.xaml", RelativePath = "Process.xaml" };
        var orphan = new UiPathWorkflowAnalysis { FileName = "Orphan.xaml", RelativePath = "Orphan.xaml" };

        var project = new ProjectScanResult { ProjectPath = "/tmp/project" };
        project.Workflows.Add(new UiPathWorkflowInfo { Name = "Main.xaml", RelativePath = "Main.xaml", FullPath = "/tmp/project/Main.xaml", Analysis = main });
        project.Workflows.Add(new UiPathWorkflowInfo { Name = "Process.xaml", RelativePath = "Process.xaml", FullPath = "/tmp/project/Process.xaml", Analysis = process });
        project.Workflows.Add(new UiPathWorkflowInfo { Name = "Orphan.xaml", RelativePath = "Orphan.xaml", FullPath = "/tmp/project/Orphan.xaml", Analysis = orphan });

        var rule = new UnusedWorkflowRule();
        var findings = rule.Analyze(new UiPathAnalysisContext { Project = project }).ToList();

        var finding = Assert.Single(findings);
        Assert.Equal("RPA037", finding.RuleId);
        Assert.Equal("Orphan.xaml", finding.WorkflowPath);
    }

    private static IReadOnlyList<UiPathAnalysisFinding> Analyze(IUiPathAnalysisRule rule, params UiPathActivityInfo[] activities)
    {
        return rule.Analyze(Context(Workflow(activities))).ToArray();
    }

    private static UiPathAnalysisContext Context(params UiPathWorkflowAnalysis[] workflows)
    {
        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/project",
            ProjectName = "TestProject",
            Compatibility = "Windows"
        };
        project.ProjectFolderExists = true;
        project.ProjectJsonExists = true;
        project.ProjectJsonParsed = true;
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

    private static UiPathWorkflowAnalysis Workflow(params UiPathActivityInfo[] activities)
    {
        var workflow = new UiPathWorkflowAnalysis
        {
            FileName = "Main.xaml",
            RelativePath = "Main.xaml"
        };
        workflow.Activities.AddRange(activities);
        return workflow;
    }

    private static UiPathActivityInfo Activity(
        string name,
        string? id = null,
        string? parentId = null,
        IReadOnlyDictionary<string, string?>? properties = null,
        IReadOnlyDictionary<string, string?>? arguments = null)
    {
        return new UiPathActivityInfo
        {
            ActivityId = id ?? Guid.NewGuid().ToString("N"),
            ParentActivityId = parentId,
            Name = name,
            DisplayName = name,
            TypeName = name,
            Namespace = name is "Click" or "TypeInto" or "GetText" ? "UiPath.Core.Activities.UiAutomation" : null,
            Depth = parentId is null ? 1 : 2,
            XamlFile = "Main.xaml",
            Properties = properties ?? new Dictionary<string, string?>(),
            Arguments = arguments ?? new Dictionary<string, string?>()
        };
    }

    private sealed class StaticFixProvider : IUiPathFixSuggestionProvider
    {
        public StaticFixProvider(IReadOnlyCollection<string> supportedRuleIds)
        {
            SupportedRuleIds = supportedRuleIds;
        }

        public IReadOnlyCollection<string> SupportedRuleIds { get; }

        public bool RequiresAi => false;

        public UiPathFixSuggestion? Suggest(UiPathFixContext context)
        {
            return null;
        }
    }
}

using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.CustomRules;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.RuleCatalog;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Reporting;
using RpaDevAssistant.Core.Reporting.Export;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathCustomRuleTests
{
    [Fact]
    public void Catalog_ReturnsBuiltInAndCustomRules()
    {
        var custom = Rule("CUSTOM-001", UiPathRuleScope.Activity, [Condition("Activity.Name", UiPathRuleConditionOperator.Equals, "Delay")]);
        var catalog = new UiPathRuleCatalog(
            [new FakeRule()],
            new BuiltInUiPathRuleProfileProvider(new InMemoryUiPathCustomRuleRepository([custom])),
            new InMemoryUiPathCustomRuleRepository([custom]),
            []);

        var rules = catalog.GetAllRules();

        Assert.Contains(rules, rule => rule.Id == "RPA999" && rule.IsBuiltIn);
        Assert.Contains(rules, rule => rule.Id == "CUSTOM-001" && !rule.IsBuiltIn);
    }

    [Fact]
    public void Evaluator_MatchesActivityRule()
    {
        var result = Analyze(Rule("CUSTOM-001", UiPathRuleScope.Activity, [Condition("Activity.Name", UiPathRuleConditionOperator.Equals, "Delay")]));

        var finding = Assert.Single(result);
        Assert.Equal("Custom", finding.Source);
        Assert.Equal("Login.xaml", finding.WorkflowPath);
        Assert.Equal("Delay", finding.ActivityName);
    }

    [Fact]
    public void Evaluator_MatchesActivityPropertyNameAndCompareValue()
    {
        var result = Analyze(Rule("CUSTOM-012", UiPathRuleScope.Activity, [
            new UiPathRuleCondition
            {
                Field = "Activity.Property",
                Operator = UiPathRuleConditionOperator.Equals,
                PropertyName = "TimeoutMS",
                CompareValue = "120000"
            }
        ]));

        var finding = Assert.Single(result);
        Assert.Equal("Click", finding.ActivityName);
    }

    [Fact]
    public void Evaluator_MatchesActivityPropertyCompactValueSyntax()
    {
        var result = Analyze(Rule("CUSTOM-013", UiPathRuleScope.Activity, [
            Condition("Activity.Property", UiPathRuleConditionOperator.Equals, "TimeoutMS=120000")
        ]));

        Assert.Single(result);
    }

    [Fact]
    public void Evaluator_MatchesActivityPropertyNumericComparison()
    {
        var result = Analyze(Rule("CUSTOM-015", UiPathRuleScope.Activity, [
            new UiPathRuleCondition
            {
                Field = "Activity.Property",
                Operator = UiPathRuleConditionOperator.GreaterThanOrEqual,
                PropertyName = "TimeoutMS",
                CompareValue = "100000"
            }
        ]));

        var finding = Assert.Single(result);
        Assert.Equal("Click", finding.ActivityName);
        Assert.Equal("TimeoutMS", finding.PropertyName);
        Assert.Equal("120000", finding.CurrentValue);
    }

    [Fact]
    public void Validator_RejectsActivityPropertyNumericComparisonWithoutNumber()
    {
        var validation = new UiPathCustomRuleValidator().Validate(Rule("CUSTOM-016", UiPathRuleScope.Activity, [
            new UiPathRuleCondition
            {
                Field = "Activity.Property",
                Operator = UiPathRuleConditionOperator.GreaterThanOrEqual,
                PropertyName = "TimeoutMS",
                CompareValue = "not-a-number"
            }
        ]));

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("must be a number", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_RejectsActivityPropertyWithoutPropertyName()
    {
        var validation = new UiPathCustomRuleValidator().Validate(Rule("CUSTOM-017", UiPathRuleScope.Activity, [
            new UiPathRuleCondition
            {
                Field = "Activity.Property",
                Operator = UiPathRuleConditionOperator.Exists,
                Value = ""
            }
        ]));

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("require propertyName", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzerLocalizer_UsesCustomRuleLocaleText()
    {
        var rule = Rule("CUSTOM-014", UiPathRuleScope.Activity, [Condition("Activity.Name", UiPathRuleConditionOperator.Equals, "Delay")]) with
        {
            NameEn = "No fixed waits",
            NameTr = "Sabit bekleme yok",
            DescriptionEn = "Delay matched.",
            DescriptionTr = "Delay eşleşti.",
            RecommendationEn = "Use state-based waiting.",
            RecommendationTr = "State-based bekleme kullanın."
        };
        var finding = Assert.Single(Analyze(rule));

        var localized = new RpaDevAssistant.Core.Localization.UiPathAnalysisFindingLocalizer(new RpaDevAssistant.Core.Localization.RpaDevAssistantLocalizer()).Localize(finding, "tr");

        Assert.Equal("Sabit bekleme yok", localized.RuleName);
        Assert.Equal("Delay eşleşti.", localized.Message);
        Assert.Equal("State-based bekleme kullanın.", localized.Recommendation);
    }

    [Fact]
    public void Evaluator_MatchesWorkflowRule()
    {
        var result = Analyze(Rule("CUSTOM-002", UiPathRuleScope.Workflow, [
            Condition("Workflow.ExecutableActivityCount", UiPathRuleConditionOperator.GreaterThanOrEqual, "2"),
            Condition("Workflow.MaxNestingDepth", UiPathRuleConditionOperator.GreaterThanOrEqual, "1")
        ]));

        Assert.Single(result);
    }

    [Fact]
    public void Evaluator_MatchModeAnyWorks()
    {
        var rule = Rule("CUSTOM-003", UiPathRuleScope.Activity, [
            Condition("Activity.Name", UiPathRuleConditionOperator.Equals, "Missing"),
            Condition("Activity.DisplayName", UiPathRuleConditionOperator.Contains, "Wait")
        ]) with { MatchMode = UiPathCustomRuleMatchMode.Any };

        Assert.Single(Analyze(rule));
    }

    [Fact]
    public void Validator_RejectsInvalidFieldAndOperator()
    {
        var validation = new UiPathCustomRuleValidator().Validate(Rule("CUSTOM-004", UiPathRuleScope.Workflow, [
            Condition("Workflow.ActivityCount", UiPathRuleConditionOperator.Contains, "10"),
            Condition("Workflow.Unknown", UiPathRuleConditionOperator.Equals, "x")
        ]));

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("not valid", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(validation.Errors, error => error.Contains("not supported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Repository_PersistsAndReloadsRules()
    {
        var path = Path.Combine(Path.GetTempPath(), $"custom-rules-{Guid.NewGuid():N}.json");
        var repository = new FileUiPathCustomRuleRepository(new UiPathCustomRuleOptions { ConfigFilePath = path });

        repository.SaveRule(Rule("CUSTOM-005", UiPathRuleScope.Activity, [Condition("Activity.Name", UiPathRuleConditionOperator.Equals, "Delay")]));
        var reloaded = new FileUiPathCustomRuleRepository(new UiPathCustomRuleOptions { ConfigFilePath = path });

        Assert.NotNull(reloaded.GetRule("CUSTOM-005"));
    }

    [Fact]
    public void Repository_ImportExportRoundTripsAndSkipsDuplicate()
    {
        var repository = new InMemoryUiPathCustomRuleRepository([Rule("CUSTOM-006", UiPathRuleScope.Activity, [Condition("Activity.Name", UiPathRuleConditionOperator.Equals, "Delay")])]);

        var result = repository.ImportRules(repository.ExportRules().Rules);

        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.SkippedDuplicateCount);
    }

    [Fact]
    public void ProfileProvider_AddsCustomRuleConfiguration()
    {
        var repository = new InMemoryUiPathCustomRuleRepository([Rule("CUSTOM-007", UiPathRuleScope.Activity, [Condition("Activity.Name", UiPathRuleConditionOperator.Equals, "Delay")]) with { Weight = 7, MaxPenalty = 11 }]);
        var profile = new BuiltInUiPathRuleProfileProvider(repository).GetProfile("default");

        var config = Assert.Single(profile.Rules.Where(rule => rule.RuleId == "CUSTOM-007"));
        Assert.Equal(7, config.Weight);
        Assert.Equal(11, config.MaxPenalty);
    }

    [Fact]
    public void ProfileProvider_ReturnsCustomProfiles()
    {
        var repository = new InMemoryUiPathRuleProfileRepository([
            new UiPathRuleProfile
            {
                Id = "company-standard",
                Name = "Company Standard",
                Rules = [new UiPathRuleConfiguration { RuleId = "RPA007", Enabled = false, Weight = 1, MaxPenalty = 10 }]
            }
        ]);
        var provider = new BuiltInUiPathRuleProfileProvider(customProfileRepository: repository);

        var profile = provider.GetProfile("company-standard");

        Assert.Equal("Company Standard", profile.Name);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA001" && rule.Enabled);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA007" && !rule.Enabled);
    }

    [Fact]
    public void FileProfileRepository_PersistsAndRejectsDefaultOverwrite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rule-profiles-{Guid.NewGuid():N}.json");
        var repository = new FileUiPathRuleProfileRepository(new UiPathRuleProfileOptions { ConfigFilePath = path });
        var saved = repository.SaveProfile(new UiPathRuleProfile
        {
            Id = "company-standard",
            Name = "Company Standard",
            Rules = [new UiPathRuleConfiguration { RuleId = "RPA007", Enabled = false, Weight = 2, MaxPenalty = 9 }]
        });

        var reloaded = new FileUiPathRuleProfileRepository(new UiPathRuleProfileOptions { ConfigFilePath = path });

        Assert.Equal(saved.Name, reloaded.GetProfile("company-standard")?.Name);
        Assert.Throws<UiPathRuleProfileValidationException>(() => repository.SaveProfile(new UiPathRuleProfile { Id = "default", Name = "Default" }));
    }

    [Fact]
    public void Scoring_UsesCustomRuleWeightAndMaxPenalty()
    {
        var finding = new UiPathAnalysisFinding
        {
            RuleId = "CUSTOM-008",
            RuleName = "Custom",
            Severity = RuleSeverity.Warning,
            Category = RuleCategory.Maintainability,
            Message = "Matched",
            Source = "Custom"
        };
        var analysis = new UiPathStaticAnalysisResult();
        analysis.Findings.AddRange([finding, finding]);
        var score = new UiPathQualityScoringEngine().Calculate(Project(), analysis, new UiPathRuleProfile
        {
            Id = "default",
            Name = "Default",
            Rules = [new UiPathRuleConfiguration { RuleId = "CUSTOM-008", Enabled = true, Weight = 10, MaxPenalty = 12 }]
        });

        Assert.Equal(12, Assert.Single(score.ScoreBreakdown).AppliedPenalty);
    }

    [Fact]
    public void Evaluator_DisabledCustomRuleDoesNotRun()
    {
        var result = Analyze(Rule("CUSTOM-009", UiPathRuleScope.Activity, [Condition("Activity.Name", UiPathRuleConditionOperator.Equals, "Delay")]) with { Enabled = false });

        Assert.Empty(result);
    }

    [Fact]
    public void Evaluator_TestReturnsNoiseWarning()
    {
        var evaluator = new UiPathCustomRuleEvaluator(new UiPathCustomRuleOptions { NoiseWarningThreshold = 1 });
        var result = evaluator.Test(Context(), Rule("CUSTOM-010", UiPathRuleScope.Activity, [Condition("Activity.Name", UiPathRuleConditionOperator.Equals, "Delay")]));

        Assert.True(result.HasNoiseWarning);
        Assert.Equal(1, result.MatchedActivityCount);
    }

    [Fact]
    public void HtmlReport_IncludesCustomFindingSource()
    {
        var project = Project();
        var analysis = new UiPathStaticAnalysisResult();
        analysis.Findings.Add(new UiPathAnalysisFinding
        {
            RuleId = "CUSTOM-011",
            RuleName = "Custom Delay",
            Severity = RuleSeverity.Warning,
            Category = RuleCategory.Maintainability,
            Message = "Delay matched",
            Source = "Custom"
        });
        var report = new UiPathAnalysisReportBuilder().Build(project, analysis, new UiPathQualityScoringEngine().Calculate(project, analysis, new UiPathRuleProfile
        {
            Id = "default",
            Name = "Default",
            Rules = [new UiPathRuleConfiguration { RuleId = "CUSTOM-011", Enabled = true, Weight = 1, MaxPenalty = 10 }]
        }), new UiPathRuleProfile { Id = "default", Name = "Default" });

        var html = new HtmlUiPathReportExporter().Export(report).Content;

        Assert.Contains("Custom", html);
        Assert.Contains("CUSTOM-011", html);
    }

    private static IReadOnlyList<UiPathAnalysisFinding> Analyze(UiPathCustomRuleDefinition rule)
    {
        return new UiPathCustomRuleEvaluator().Analyze(Context(), [rule]);
    }

    private static UiPathAnalysisContext Context()
    {
        return new UiPathAnalysisContext { Project = Project() };
    }

    private static ProjectScanResult Project()
    {
        var workflow = new UiPathWorkflowAnalysis
        {
            FileName = "Login.xaml",
            RelativePath = "Login.xaml"
        };
        workflow.Activities.Add(new UiPathActivityInfo
        {
            ActivityId = "a1",
            Name = "Delay",
            DisplayName = "Wait for page",
            TypeName = "Delay",
            XamlFile = "Login.xaml",
            Depth = 1
        });
        workflow.Activities.Add(new UiPathActivityInfo
        {
            ActivityId = "a2",
            Name = "Click",
            DisplayName = "Click Login",
            TypeName = "Click",
            XamlFile = "Login.xaml",
            Depth = 2,
            Properties = new Dictionary<string, string?> { ["TimeoutMS"] = "120000" }
        });
        workflow.Complexity = new UiPathWorkflowMetricsCalculator().CalculateComplexity(workflow);

        var project = new ProjectScanResult { ProjectPath = "/tmp/project", ProjectJsonExists = true, ProjectJsonParsed = true, ProjectFolderExists = true };
        project.Workflows.Add(new UiPathWorkflowInfo
        {
            Name = "Login.xaml",
            RelativePath = "Login.xaml",
            FullPath = "/tmp/project/Login.xaml",
            Analysis = workflow
        });
        return project;
    }

    private static UiPathCustomRuleDefinition Rule(string id, UiPathRuleScope scope, IReadOnlyList<UiPathRuleCondition> conditions)
    {
        return new UiPathCustomRuleDefinition
        {
            Id = id,
            Name = "Custom Rule",
            Description = "Custom match",
            Recommendation = "Review the match.",
            Category = RuleCategory.Maintainability,
            Severity = RuleSeverity.Warning,
            Scope = scope,
            Enabled = true,
            Weight = 1,
            MaxPenalty = 10,
            Conditions = conditions
        };
    }

    private static UiPathRuleCondition Condition(string field, UiPathRuleConditionOperator op, string value)
    {
        return new UiPathRuleCondition { Field = field, Operator = op, Value = value };
    }

    private sealed class FakeRule : IUiPathAnalysisRule
    {
        public string Id => "RPA999";
        public string Name => "Fake";
        public string Description => "Fake";
        public RuleSeverity Severity => RuleSeverity.Warning;
        public RuleCategory Category => RuleCategory.Maintainability;
        public IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context) => [];
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.CustomRules;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.RuleCatalog;
using RpaDevAssistant.Core.Analysis.Rules;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Dependencies;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.ProjectAssistant;
using RpaDevAssistant.Core.Reporting;
using RpaDevAssistant.Core.Reporting.Export;
using RpaDevAssistant.Core.Scanning;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathDependencyAnalysisTests
{
    [Fact]
    public void Scanner_ParsesDependencyRawVersionAndAnalysis()
    {
        using var fixture = new DependencyProjectFixture();

        var result = new UiPathProjectScanner().Scan(fixture.RootPath);

        Assert.Contains(result.Dependencies, dependency => dependency.Name == "UiPath.Excel.Activities" && dependency.Version == "[23.10.3]");
        Assert.NotNull(result.DependencyAnalysis);
        Assert.Equal(5, result.DependencyAnalysis!.TotalDependencies);
    }

    [Fact]
    public void PackageMapper_ClassifiesKnownFamilies()
    {
        var mapper = new UiPathPackageActivityMapper();

        Assert.Equal(UiPathPackageCategory.Excel, mapper.ClassifyPackage("UiPath.Excel.Activities").Category);
        Assert.Equal(UiPathPackageCategory.Mail, mapper.ClassifyPackage("UiPath.Mail.Activities").Category);
        Assert.True(mapper.ClassifyPackage("UiPath.System.Activities").IsUiPathPackage);
    }

    [Fact]
    public void DependencyAnalyzer_DetectsUsedAndPossiblyUnusedPackages()
    {
        var project = Project();
        var summary = new UiPathDependencyAnalyzer().Analyze(project);

        var excel = Assert.Single(summary.Packages.Where(package => package.Name == "UiPath.Excel.Activities"));
        Assert.Equal(UiPathDependencyUsageStatus.Used, excel.UsageStatus);
        Assert.Contains("UseExcelFile", excel.UsedActivities);
        Assert.Contains("Main.xaml", excel.UsedByWorkflows);

        var mail = Assert.Single(summary.Packages.Where(package => package.Name == "UiPath.Mail.Activities"));
        Assert.Equal(UiPathDependencyUsageStatus.PossiblyUnused, mail.UsageStatus);
        Assert.NotEmpty(mail.Findings);

        var custom = Assert.Single(summary.Packages.Where(package => package.Name == "Contoso.Custom.Activities"));
        Assert.Equal(UiPathDependencyUsageStatus.Unknown, custom.UsageStatus);
    }

    [Fact]
    public void DependencyAnalyzer_DetectsVersionAlignmentAndMixedMode()
    {
        var summary = new UiPathDependencyAnalyzer().Analyze(Project());

        Assert.Equal(UiPathModernClassicMode.Mixed, summary.ModernClassicMode);
        Assert.Contains(summary.Packages, package => package.Name == "UiPath.UIAutomation.Activities" && package.CompatibilityStatus == UiPathDependencyCompatibilityStatus.PotentialConflict);
        Assert.True(summary.PotentialConflicts > 0);
    }

    [Fact]
    public void DependencyRules_ProduceFindingsFromSummary()
    {
        var context = new UiPathAnalysisContext { Project = Project() };

        Assert.Contains(new PossiblyUnusedDependencyRule().Analyze(context), finding => finding.RuleId == "RPA026" && finding.CurrentValue == "UiPath.Mail.Activities");
        Assert.Contains(new PackageVersionAlignmentRiskRule().Analyze(context), finding => finding.RuleId == "RPA027");
        Assert.Contains(new MixedModernClassicActivityUsageRule().Analyze(context), finding => finding.RuleId == "RPA028");
    }

    [Fact]
    public async Task AskProject_AnswersDependencyQuestionsLocally()
    {
        using var fixture = new DependencyProjectFixture();
        var service = CreateQuestionService();

        var answer = await service.AskAsync(new UiPathProjectQuestion
        {
            ProjectPath = fixture.RootPath,
            Question = "Hangi package'lar kullanılmıyor olabilir?",
            Locale = "tr"
        }, CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("Kullanılmıyor", answer.Answer);
        Assert.Contains("UiPath.Mail.Activities", answer.Answer);
    }

    [Fact]
    public void HtmlReport_IncludesDependencyAnalysisSection()
    {
        var project = Project();
        project.DependencyAnalysis = new UiPathDependencyAnalyzer().Analyze(project);
        var analysis = new UiPathStaticAnalysisResult();
        var profile = new UiPathRuleProfile { Id = "default", Name = "Default" };
        var score = new UiPathQualityScoringEngine().Calculate(project, analysis, profile);
        var report = new UiPathAnalysisReportBuilder().Build(project, analysis, score, profile);

        var html = new HtmlUiPathReportExporter().Export(report, "en").Content;

        Assert.Contains("Dependency Analysis", html);
        Assert.Contains("UiPath.Excel.Activities", html);
    }

    [Fact]
    public void CustomRule_EvaluatesDependencyConditionsOnSamePackage()
    {
        var project = Project();
        project.DependencyAnalysis = new UiPathDependencyAnalyzer().Analyze(project);
        var context = new UiPathAnalysisContext { Project = project };
        var rule = new UiPathCustomRuleDefinition
        {
            Id = "CUSTOM-DEP-001",
            Name = "Unused Mail",
            Description = "Mail package might be unused.",
            Recommendation = "Review package usage.",
            Scope = UiPathRuleScope.Project,
            Category = RuleCategory.Configuration,
            Severity = RuleSeverity.Suggestion,
            Enabled = true,
            MatchMode = UiPathCustomRuleMatchMode.All,
            Conditions =
            [
                new UiPathRuleCondition { Field = "Dependency.Name", Operator = UiPathRuleConditionOperator.Equals, Value = "UiPath.Mail.Activities" },
                new UiPathRuleCondition { Field = "Dependency.UsageStatus", Operator = UiPathRuleConditionOperator.Equals, Value = "PossiblyUnused" }
            ]
        };

        var finding = Assert.Single(new UiPathCustomRuleEvaluator().Analyze(context, [rule]));

        Assert.Equal("CUSTOM-DEP-001", finding.RuleId);
        Assert.Contains("UiPath.Mail.Activities", finding.CurrentValue);
    }

    private static UiPathProjectQuestionService CreateQuestionService()
    {
        var scanner = new UiPathProjectScanner();
        var analyzer = new UiPathProjectAnalyzer(
            scanner,
            new UiPathRuleEngine([
                new PossiblyUnusedDependencyRule(),
                new PackageVersionAlignmentRiskRule(),
                new MixedModernClassicActivityUsageRule(),
                new LegacyPackageIndicatorRule()
            ]),
            new BuiltInUiPathRuleProfileProvider(),
            new UiPathQualityScoringEngine());
        return new UiPathProjectQuestionService(
            analyzer,
            new UiPathProjectQuestionClassifier(),
            new UiPathProjectRetriever(new SensitiveValueRedactor()),
            new UiPathWorkflowGraphBuilder(),
            new UiPathProjectAssistantPromptBuilder(),
            new FakeAssistantProvider(),
            NullLogger<UiPathProjectQuestionService>.Instance);
    }

    private static ProjectScanResult Project()
    {
        var workflow = new UiPathWorkflowAnalysis { FileName = "Main.xaml", RelativePath = "Main.xaml" };
        workflow.Activities.Add(new UiPathActivityInfo
        {
            ActivityId = "1",
            Name = "UseExcelFile",
            DisplayName = "Use Excel File",
            TypeName = "UseExcelFile",
            Namespace = "http://schemas.uipath.com/workflow/activities/excel",
            XamlFile = "Main.xaml"
        });
        workflow.Activities.Add(new UiPathActivityInfo
        {
            ActivityId = "2",
            Name = "UseApplicationBrowser",
            DisplayName = "Use Application Browser",
            TypeName = "UseApplicationBrowser",
            Namespace = "http://schemas.uipath.com/workflow/activities/uiautomation",
            XamlFile = "Main.xaml"
        });
        workflow.Activities.Add(new UiPathActivityInfo
        {
            ActivityId = "3",
            Name = "OpenBrowser",
            DisplayName = "Open Browser",
            TypeName = "OpenBrowser",
            Namespace = "http://schemas.uipath.com/workflow/activities/uiautomation",
            XamlFile = "Main.xaml"
        });

        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/project",
            ProjectFolderExists = true,
            ProjectJsonExists = true,
            ProjectJsonParsed = true,
            ProjectName = "DependencyProject"
        };
        project.Dependencies.AddRange([
            new UiPathDependency { Name = "UiPath.System.Activities", Version = "[25.4.1]" },
            new UiPathDependency { Name = "UiPath.UIAutomation.Activities", Version = "[20.10.6]" },
            new UiPathDependency { Name = "UiPath.Excel.Activities", Version = "[23.10.3]" },
            new UiPathDependency { Name = "UiPath.Mail.Activities", Version = "[23.10.1]" },
            new UiPathDependency { Name = "Contoso.Custom.Activities", Version = "1.0.0" }
        ]);
        project.Workflows.Add(new UiPathWorkflowInfo
        {
            Name = "Main.xaml",
            RelativePath = "Main.xaml",
            FullPath = "/tmp/project/Main.xaml",
            Analysis = workflow
        });
        project.DependencyAnalysis = new UiPathDependencyAnalyzer().Analyze(project);
        return project;
    }

    private sealed class DependencyProjectFixture : IDisposable
    {
        public DependencyProjectFixture()
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"RpaDependencyProject-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
            File.WriteAllText(Path.Combine(RootPath, "project.json"), """
            {
              "name": "DependencyProject",
              "compatibility": "Windows",
              "dependencies": {
                "UiPath.System.Activities": "[25.4.1]",
                "UiPath.UIAutomation.Activities": "[20.10.6]",
                "UiPath.Excel.Activities": "[23.10.3]",
                "UiPath.Mail.Activities": "[23.10.1]",
                "Contoso.Custom.Activities": "1.0.0"
              }
            }
            """);
            File.WriteAllText(Path.Combine(RootPath, "Main.xaml"), """
            <Activity xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                      xmlns:ui="http://schemas.uipath.com/workflow/activities"
                      x:Class="Main">
              <Sequence DisplayName="Main">
                <Sequence.Activities>
                  <ui:UseExcelFile DisplayName="Use Excel File" />
                  <ui:UseApplicationBrowser DisplayName="Use Application Browser" />
                  <ui:OpenBrowser DisplayName="Open Browser" />
                </Sequence.Activities>
              </Sequence>
            </Activity>
            """);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private sealed class FakeAssistantProvider : IUiPathProjectAssistantAiProvider
    {
        public bool IsConfigured => false;

        public string ProviderName => "Fake";

        public Task<UiPathProjectAnswer> AnswerAsync(UiPathProjectAssistantPrompt prompt, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("AI should not be called for dependency local answers.");
        }
    }
}

using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Models;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathQualityScoringEngineTests
{
    [Fact]
    public void Calculate_Returns100AndA_WhenThereAreNoFindings()
    {
        var score = Calculate(Project(), Analysis(), Profile(Config("RPA001", weight: 2, maxPenalty: 10)));

        Assert.Equal(100, score.Score);
        Assert.Equal("A", score.Grade);
    }

    [Fact]
    public void Calculate_DecreasesScore_ForSingleWarning()
    {
        var score = Calculate(Project(workflowCount: 1, totalActivityCount: 0), Analysis(Finding("RPA001", RuleSeverity.Warning)), Profile(Config("RPA001", weight: 10, maxPenalty: 30)));

        Assert.Equal(90, score.Score);
    }

    [Fact]
    public void Calculate_PenalizesErrorMoreThanWarning()
    {
        var profile = Profile(Config("RPA001", weight: 10, maxPenalty: 30));

        var warningScore = Calculate(Project(workflowCount: 1, totalActivityCount: 0), Analysis(Finding("RPA001", RuleSeverity.Warning)), profile);
        var errorScore = Calculate(Project(workflowCount: 1, totalActivityCount: 0), Analysis(Finding("RPA001", RuleSeverity.Error)), profile);

        Assert.True(errorScore.Score < warningScore.Score);
    }

    [Fact]
    public void Calculate_AppliesHighestMultiplierForCritical()
    {
        var score = Calculate(Project(workflowCount: 1, totalActivityCount: 0), Analysis(Finding("RPA001", RuleSeverity.Critical)), Profile(Config("RPA001", weight: 10, maxPenalty: 30)));

        Assert.Equal(80, score.Score);
        Assert.Equal(20, score.RawPenalty);
    }

    [Fact]
    public void Calculate_AppliesMaxPenalty()
    {
        var score = Calculate(
            Project(workflowCount: 1, totalActivityCount: 0),
            Analysis(
                Finding("RPA001", RuleSeverity.Error),
                Finding("RPA001", RuleSeverity.Error),
                Finding("RPA001", RuleSeverity.Error)),
            Profile(Config("RPA001", weight: 10, maxPenalty: 20)));

        var breakdown = Assert.Single(score.ScoreBreakdown);
        Assert.Equal(45, breakdown.RawPenalty);
        Assert.Equal(20, breakdown.AppliedPenalty);
        Assert.Equal(80, score.Score);
    }

    [Fact]
    public void Calculate_IgnoresDisabledRule()
    {
        var score = Calculate(Project(), Analysis(Finding("RPA001", RuleSeverity.Critical)), Profile(Config("RPA001", enabled: false, weight: 50, maxPenalty: 100)));

        Assert.Equal(100, score.Score);
        Assert.Empty(score.ScoreBreakdown);
    }

    [Fact]
    public void Calculate_AppliesSeverityOverride()
    {
        var score = Calculate(
            Project(workflowCount: 1, totalActivityCount: 0),
            Analysis(Finding("RPA001", RuleSeverity.Warning)),
            Profile(Config("RPA001", weight: 10, maxPenalty: 30, severityOverride: RuleSeverity.Critical)));

        var breakdown = Assert.Single(score.ScoreBreakdown);
        Assert.Equal(RuleSeverity.Critical, breakdown.Severity);
        Assert.Equal(20, breakdown.RawPenalty);
    }

    [Fact]
    public void Calculate_DoesNotDropBelowZero()
    {
        var score = Calculate(Project(workflowCount: 1, totalActivityCount: 0), Analysis(Finding("RPA001", RuleSeverity.Critical)), Profile(Config("RPA001", weight: 500, maxPenalty: 500)));

        Assert.Equal(0, score.Score);
    }

    [Fact]
    public void Calculate_DoesNotGoAbove100()
    {
        var score = Calculate(Project(), Analysis(Finding("RPA001", RuleSeverity.Info)), Profile(Config("RPA001", weight: 10, maxPenalty: 30)));

        Assert.Equal(100, score.Score);
    }

    [Theory]
    [InlineData(100, "A")]
    [InlineData(90, "A")]
    [InlineData(89, "B")]
    [InlineData(80, "B")]
    [InlineData(79, "C")]
    [InlineData(70, "C")]
    [InlineData(69, "D")]
    [InlineData(60, "D")]
    [InlineData(59, "F")]
    public void CalculateGrade_ReturnsExpectedGrade(int score, string expectedGrade)
    {
        Assert.Equal(expectedGrade, UiPathQualityScoringEngine.CalculateGrade(score));
    }

    [Fact]
    public void Calculate_NormalizesPenaltyDownForLargeProjects()
    {
        var analysis = Analysis(Finding("RPA001", RuleSeverity.Warning), Finding("RPA001", RuleSeverity.Warning), Finding("RPA001", RuleSeverity.Warning));
        var profile = Profile(Config("RPA001", weight: 10, maxPenalty: 100));

        var smallProjectScore = Calculate(Project(workflowCount: 2, totalActivityCount: 10), analysis, profile);
        var largeProjectScore = Calculate(Project(workflowCount: 100, totalActivityCount: 1_000), analysis, profile);

        Assert.True(largeProjectScore.NormalizedPenalty < smallProjectScore.NormalizedPenalty);
        Assert.True(largeProjectScore.Score > smallProjectScore.Score);
    }

    [Fact]
    public void Calculate_AppliesStrongerPenaltyForSmallProjects()
    {
        var score = Calculate(
            Project(workflowCount: 1, totalActivityCount: 0),
            Analysis(Finding("RPA001", RuleSeverity.Warning)),
            Profile(Config("RPA001", weight: 10, maxPenalty: 100)));

        Assert.Equal(score.RawPenalty, score.NormalizedPenalty);
    }

    [Fact]
    public void Calculate_ReturnsScoreBreakdown()
    {
        var score = Calculate(
            Project(workflowCount: 1, totalActivityCount: 0),
            Analysis(Finding("RPA002", RuleSeverity.Error), Finding("RPA002", RuleSeverity.Error)),
            Profile(Config("RPA002", weight: 10, maxPenalty: 20)));

        var breakdown = Assert.Single(score.ScoreBreakdown);
        Assert.Equal("RPA002", breakdown.RuleId);
        Assert.Equal("Test Rule", breakdown.RuleName);
        Assert.Equal(2, breakdown.FindingCount);
        Assert.Equal(2, breakdown.OccurrenceCount);
        Assert.Equal(RuleSeverity.Error, breakdown.Severity);
        Assert.Equal(10, breakdown.Weight);
        Assert.Equal(30, breakdown.RawPenalty);
        Assert.Equal(20, breakdown.AppliedPenalty);
        Assert.Equal(20, breakdown.MaxPenalty);
    }

    [Fact]
    public void Calculate_ReturnsSeverityBreakdownAndProjectSizeFactor()
    {
        var score = Calculate(
            Project(workflowCount: 10, totalActivityCount: 100),
            Analysis(Finding("RPA001", RuleSeverity.Warning), Finding("RPA002", RuleSeverity.Error)),
            Profile(Config("RPA001", weight: 2, maxPenalty: 10), Config("RPA002", weight: 10, maxPenalty: 30)));

        Assert.True(score.ProjectSizeFactor > 1);
        Assert.Equal(2, score.SeverityBreakdown.Count);
        Assert.Contains(score.SeverityBreakdown, item => item.Severity == RuleSeverity.Warning && item.FindingCount == 1);
        Assert.Contains(score.SeverityBreakdown, item => item.Severity == RuleSeverity.Error && item.AppliedPenalty == 15);
    }

    [Fact]
    public void Calculate_ScoresAggregatedFindingByFindingCountAndKeepsOccurrenceCount()
    {
        var score = Calculate(
            Project(workflowCount: 1, totalActivityCount: 50),
            Analysis(Finding("RPA007", RuleSeverity.Suggestion) with
            {
                Scope = UiPathFindingScope.Aggregated,
                OccurrenceCount = 50,
                AffectedActivityCount = 50
            }),
            Profile(Config("RPA007", weight: 1, maxPenalty: 10)));

        var breakdown = Assert.Single(score.ScoreBreakdown);
        Assert.Equal(1, breakdown.FindingCount);
        Assert.Equal(50, breakdown.OccurrenceCount);
        Assert.Equal(0.5, breakdown.RawPenalty);
        Assert.Equal(0.5, breakdown.AppliedPenalty);
    }

    [Fact]
    public void ProfileProvider_ThrowsMeaningfulError_ForUnknownProfileId()
    {
        var provider = new BuiltInUiPathRuleProfileProvider();

        var exception = Assert.Throws<UnknownRuleProfileException>(() => provider.GetProfile("missing"));
        Assert.Equal("missing", exception.ProfileId);
    }

    [Fact]
    public void ProfileProvider_UsesDefaultProfile_WhenProfileIdIsMissing()
    {
        var provider = new BuiltInUiPathRuleProfileProvider();

        var profile = provider.GetProfile(null);

        Assert.Equal("default", profile.Id);
        Assert.Equal("Default", profile.Name);
    }

    private static UiPathQualityScore Calculate(ProjectScanResult project, UiPathStaticAnalysisResult analysis, UiPathRuleProfile profile)
    {
        return new UiPathQualityScoringEngine().Calculate(project, analysis, profile);
    }

    private static ProjectScanResult Project(int workflowCount = 1, int totalActivityCount = 0)
    {
        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/project",
            ProjectFolderExists = true,
            ProjectJsonExists = true,
            ProjectJsonParsed = true
        };

        for (var workflowIndex = 0; workflowIndex < workflowCount; workflowIndex++)
        {
            var workflowAnalysis = new UiPathWorkflowAnalysis
            {
                FileName = $"Workflow{workflowIndex}.xaml",
                RelativePath = $"Workflow{workflowIndex}.xaml"
            };

            var activitiesInWorkflow = workflowIndex == 0 ? totalActivityCount : 0;
            for (var activityIndex = 0; activityIndex < activitiesInWorkflow; activityIndex++)
            {
                workflowAnalysis.Activities.Add(new UiPathActivityInfo
                {
                    ActivityId = $"{workflowIndex}-{activityIndex}",
                    Name = "Assign",
                    DisplayName = "Assign value",
                    TypeName = "Assign",
                    Depth = 0,
                    XamlFile = workflowAnalysis.RelativePath
                });
            }

            project.Workflows.Add(new UiPathWorkflowInfo
            {
                Name = workflowAnalysis.FileName,
                RelativePath = workflowAnalysis.RelativePath,
                FullPath = $"/tmp/project/{workflowAnalysis.FileName}",
                Analysis = workflowAnalysis
            });
        }

        return project;
    }

    private static UiPathStaticAnalysisResult Analysis(params UiPathAnalysisFinding[] findings)
    {
        var result = new UiPathStaticAnalysisResult();
        result.Findings.AddRange(findings);
        return result;
    }

    private static UiPathAnalysisFinding Finding(string ruleId, RuleSeverity severity)
    {
        return new UiPathAnalysisFinding
        {
            RuleId = ruleId,
            RuleName = "Test Rule",
            Severity = severity,
            Category = RuleCategory.Reliability,
            Message = "Test finding"
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

    private static UiPathRuleConfiguration Config(
        string ruleId,
        bool enabled = true,
        double weight = 1,
        double maxPenalty = 10,
        RuleSeverity? severityOverride = null)
    {
        return new UiPathRuleConfiguration
        {
            RuleId = ruleId,
            Enabled = enabled,
            Weight = weight,
            MaxPenalty = maxPenalty,
            SeverityOverride = severityOverride
        };
    }
}

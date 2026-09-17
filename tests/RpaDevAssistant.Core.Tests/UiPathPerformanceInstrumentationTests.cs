using RpaDevAssistant.Api.Responses;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Parsing;
using RpaDevAssistant.Core.Scanning;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathPerformanceInstrumentationTests
{
    [Fact]
    public void Scanner_ReportsWorkflowDiscoveryAndXamlParsingTimings()
    {
        using var project = new PerformanceProjectFixture();
        var parser = new DelayedParser();

        var result = new UiPathProjectScanner(parser).Scan(project.RootPath);

        Assert.Equal(2, parser.ParseCount);
        Assert.True(result.Performance.TotalElapsedMilliseconds > 0);
        Assert.True(result.Performance.WorkflowDiscoveryElapsedMilliseconds >= 0);
        Assert.True(result.Performance.XamlParsingElapsedMilliseconds >= 10);
        Assert.True(result.Performance.TotalElapsedMilliseconds >= result.Performance.XamlParsingElapsedMilliseconds);
    }

    [Fact]
    public void Analyzer_ReportsScanRulesScoringAndTotalTimings()
    {
        var scanner = new FixedScanner();
        var analyzer = new UiPathProjectAnalyzer(
            scanner,
            new DelayedRuleEngine(),
            new FixedProfileProvider(),
            new DelayedScoringEngine());

        var result = analyzer.Analyze("/project");

        Assert.Equal(4.5, result.Performance.ScanElapsedMilliseconds);
        Assert.Equal(1.25, result.Performance.WorkflowDiscoveryElapsedMilliseconds);
        Assert.Equal(2.75, result.Performance.XamlParsingElapsedMilliseconds);
        Assert.True(result.Performance.RuleAnalysisElapsedMilliseconds >= 8);
        Assert.True(result.Performance.ScoringElapsedMilliseconds >= 8);
        Assert.True(result.Performance.TotalElapsedMilliseconds >=
                    result.Performance.RuleAnalysisElapsedMilliseconds + result.Performance.ScoringElapsedMilliseconds);
    }

    [Fact]
    public void AnalyzeResponse_ExposesPerformanceMetrics()
    {
        var performance = new UiPathAnalysisPerformanceMetrics
        {
            TotalElapsedMilliseconds = 25,
            ScanElapsedMilliseconds = 15,
            WorkflowDiscoveryElapsedMilliseconds = 2,
            XamlParsingElapsedMilliseconds = 10,
            RuleAnalysisElapsedMilliseconds = 7,
            ScoringElapsedMilliseconds = 1
        };
        var result = new UiPathProjectAnalysisResult
        {
            ProjectScan = FixedScanner.Project(),
            Analysis = new UiPathStaticAnalysisResult(),
            QualityScore = Score(),
            Profile = FixedProfileProvider.Profile,
            Performance = performance
        };

        var response = AnalyzeUiPathProjectResponse.From(result);

        Assert.Equal(performance, response.Performance);
    }

    private static UiPathQualityScore Score() => new()
    {
        Score = 100,
        Grade = "A",
        ProfileId = "default",
        ProfileName = "Default"
    };

    private sealed class DelayedParser : IUiPathXamlParser
    {
        public int ParseCount { get; private set; }

        public UiPathWorkflowAnalysis Parse(string xamlPath, string projectRoot)
        {
            ParseCount++;
            Thread.Sleep(6);
            return new UiPathWorkflowAnalysis
            {
                FileName = Path.GetFileName(xamlPath),
                RelativePath = Path.GetRelativePath(projectRoot, xamlPath).Replace(Path.DirectorySeparatorChar, '/')
            };
        }
    }

    private sealed class FixedScanner : IUiPathProjectScanner
    {
        public ProjectScanResult Scan(string projectPath) => Project();

        public static ProjectScanResult Project() => new()
        {
            ProjectPath = "/project",
            ProjectFolderExists = true,
            ProjectJsonExists = true,
            ProjectJsonParsed = true,
            Performance = new UiPathScanPerformanceMetrics
            {
                TotalElapsedMilliseconds = 4.5,
                WorkflowDiscoveryElapsedMilliseconds = 1.25,
                XamlParsingElapsedMilliseconds = 2.75
            }
        };
    }

    private sealed class DelayedRuleEngine : IUiPathRuleEngine
    {
        public UiPathStaticAnalysisResult Analyze(UiPathAnalysisContext context, UiPathRuleProfile? profile = null)
        {
            Thread.Sleep(10);
            return new UiPathStaticAnalysisResult();
        }
    }

    private sealed class DelayedScoringEngine : IUiPathQualityScoringEngine
    {
        public UiPathQualityScore Calculate(ProjectScanResult project, UiPathStaticAnalysisResult analysis, UiPathRuleProfile profile)
        {
            Thread.Sleep(10);
            return Score();
        }
    }

    private sealed class FixedProfileProvider : IUiPathRuleProfileProvider
    {
        public static UiPathRuleProfile Profile { get; } = new() { Id = "default", Name = "Default" };

        public IReadOnlyList<UiPathRuleProfile> GetProfiles() => [Profile];

        public UiPathRuleProfile GetProfile(string? profileId) => Profile;
    }

    private sealed class PerformanceProjectFixture : IDisposable
    {
        public string RootPath { get; } = Path.Combine(Path.GetTempPath(), $"RpaPerformance-{Guid.NewGuid():N}");

        public PerformanceProjectFixture()
        {
            Directory.CreateDirectory(RootPath);
            File.WriteAllText(Path.Combine(RootPath, "project.json"), "{\"name\":\"PerformanceFixture\",\"dependencies\":{}}");
            File.WriteAllText(Path.Combine(RootPath, "Main.xaml"), "<Sequence />");
            File.WriteAllText(Path.Combine(RootPath, "Process.xaml"), "<Sequence />");
        }

        public void Dispose()
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}

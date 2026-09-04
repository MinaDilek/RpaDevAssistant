using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Rules;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Fixes.Providers;
using RpaDevAssistant.Core.Parsing;
using RpaDevAssistant.Core.ProjectAssistant;
using RpaDevAssistant.Core.Reporting;
using RpaDevAssistant.Core.Reporting.Export;
using RpaDevAssistant.Core.Scanning;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class RealUiPathProjectEndToEndValidationTests
{
    private const string SecretFixtureValue = "FakeSecret123!";

    [Fact]
    public async Task RealisticValidationProject_EndToEndPipeline_MatchesManifestAndProducesReport()
    {
        var projectPath = FixturePath();
        var manifest = LoadManifest(projectPath);
        var services = CreateServices();

        var result = services.Analyzer.Analyze(projectPath);
        var report = services.ReportBuilder.Build(result.ProjectScan, result.Analysis, result.QualityScore, result.Profile);
        var metrics = CalculateMetrics(manifest, result.Analysis.Findings);

        Assert.True(result.ProjectScan.IsValid, string.Join(Environment.NewLine, result.ProjectScan.Errors));
        Assert.True(result.ProjectScan.IsReFramework);
        Assert.True(result.WorkflowCount >= 12);
        Assert.InRange(result.QualityScore.Score, manifest.ExpectedScoreRange.Min, manifest.ExpectedScoreRange.Max);
        Assert.True(metrics.Recall >= 0.90, $"Recall was {metrics.Recall:P2}");
        Assert.True(metrics.Precision >= 0.90, $"Precision was {metrics.Precision:P2}. Unexpected: {string.Join(", ", metrics.UnexpectedFindings.Select(Describe))}");

        foreach (var expected in manifest.ExpectedFindings)
        {
            var matches = Find(result.Analysis.Findings, expected.RuleId, expected.WorkflowPath).ToArray();
            Assert.True(matches.Length >= expected.MinimumCount, $"{expected.RuleId} {expected.WorkflowPath} expected {expected.MinimumCount}, found {matches.Length}");
            if (Enum.TryParse<RuleSeverity>(expected.Severity, out var severity))
            {
                Assert.All(matches, finding => Assert.Equal(severity, finding.Severity));
            }

            if (!string.IsNullOrWhiteSpace(expected.ExpectedCurrentValue))
            {
                Assert.Contains(matches, finding => finding.CurrentValue == expected.ExpectedCurrentValue);
            }
        }

        foreach (var expectedNoFinding in manifest.ExpectedNoFindings)
        {
            Assert.Empty(Find(result.Analysis.Findings, expectedNoFinding.RuleId, expectedNoFinding.WorkflowPath));
        }

        Assert.DoesNotContain(result.Analysis.Findings, finding => string.Equals(finding.CurrentValue, SecretFixtureValue, StringComparison.Ordinal));
        ValidateInvocationGraph(result);
        await ValidateAskProjectAsync(services.QuestionService, projectPath, result);
        await ValidateAiReviewRedactionAsync(services, projectPath);
        ValidateReports(report, result.Analysis.TotalFindings);
        WriteValidationArtifacts(projectPath, result, metrics);
    }

    [Fact]
    public async Task RealisticValidationProject_ApplyReanalyzeUndo_WorksOnTemporaryCopy()
    {
        using var copy = FixtureCopy();
        var services = CreateServices();
        var before = services.Analyzer.Analyze(copy.RootPath);
        var rpa007 = before.Analysis.Findings.First(finding =>
            finding.RuleId == "RPA007" &&
            finding.WorkflowPath == "Business/Login.xaml" &&
            finding.AffectedActivities.Any(activity => activity.ActivityDisplayName == "Click"));
        var rpa007Activity = rpa007.AffectedActivities.First(activity => activity.ActivityDisplayName == "Click");

        var suggestionResult = await services.FixSuggestions.SuggestAsync(new UiPathFixSuggestionRequest
        {
            ProjectPath = copy.RootPath,
            RuleId = rpa007.RuleId,
            WorkflowPath = rpa007.WorkflowPath,
            ActivityId = rpa007Activity.ActivityId,
            PropertyName = rpa007.PropertyName
        }, CancellationToken.None);
        var suggestion = Assert.IsType<UiPathFixSuggestion>(suggestionResult.Suggestion);

        Assert.False(suggestion.RequiresAi);
        Assert.True(suggestion.CanAutoApply);
        Assert.Equal(UiPathFixRiskLevel.Low, suggestion.RiskLevel);
        Assert.Equal("DisplayName = \"Click\"", suggestion.BeforePreview);
        Assert.False(string.IsNullOrWhiteSpace(suggestion.AfterPreview));
        Assert.False(string.IsNullOrWhiteSpace(suggestion.ExpectedFileHash));

        var apply = await services.Applier.ApplyAsync(new UiPathFixApplyRequest
        {
            ProjectPath = copy.RootPath,
            FixSuggestionId = suggestion.Id,
            RuleId = suggestion.RuleId,
            WorkflowPath = suggestion.WorkflowPath!,
            ActivityId = suggestion.ActivityId,
            PropertyName = suggestion.PropertyName!,
            ExpectedCurrentValue = suggestion.CurrentValue,
            SuggestedValue = suggestion.SuggestedValue!,
            ExpectedFileHash = suggestion.ExpectedFileHash
        }, CancellationToken.None);

        Assert.True(apply.Success, apply.Message);
        Assert.True(apply.Applied);
        Assert.True(apply.RequiresReanalysis);
        Assert.NotNull(apply.BackupPath);

        var afterApply = services.Analyzer.Analyze(copy.RootPath);
        Assert.DoesNotContain(afterApply.Analysis.Findings, finding =>
            finding.RuleId == "RPA007" &&
            finding.WorkflowPath == "Business/Login.xaml" &&
            finding.AffectedActivities.Any(activity => activity.ActivityId == rpa007Activity.ActivityId));
        Assert.True(afterApply.QualityScore.Score >= before.QualityScore.Score);

        var backups = services.BackupRepository.ListBackups(copy.RootPath);
        var backup = Assert.Single(backups.Where(item => item.WorkflowPath == "Business/Login.xaml"));
        Assert.True(backup.CanUndo, backup.Reason);

        var undo = await services.Undo.UndoAsync(new UiPathUndoRequest
        {
            ProjectPath = copy.RootPath,
            BackupId = backup.BackupId,
            WorkflowPath = backup.WorkflowPath!,
            ExpectedCurrentHash = backup.ModifiedHash
        }, CancellationToken.None);

        Assert.True(undo.Success, undo.Message);
        Assert.True(undo.Restored);
        Assert.True(undo.RequiresReanalysis);
        Assert.NotNull(undo.SafetyBackupId);

        var afterUndo = services.Analyzer.Analyze(copy.RootPath);
        Assert.Contains(afterUndo.Analysis.Findings, finding =>
            finding.RuleId == "RPA007" &&
            finding.WorkflowPath == "Business/Login.xaml" &&
            finding.AffectedActivities.Any(activity => activity.ActivityDisplayName == "Click"));
    }

    [Fact]
    public async Task RealisticValidationProject_ExternalModificationBlocksUndo()
    {
        using var copy = FixtureCopy();
        var services = CreateServices();
        var analysis = services.Analyzer.Analyze(copy.RootPath);
        var rpa007 = analysis.Analysis.Findings.First(finding => finding.RuleId == "RPA007" && finding.WorkflowPath == "Business/Login.xaml");
        var rpa007Activity = rpa007.AffectedActivities.First();
        var suggestion = (await services.FixSuggestions.SuggestAsync(new UiPathFixSuggestionRequest
        {
            ProjectPath = copy.RootPath,
            RuleId = rpa007.RuleId,
            WorkflowPath = rpa007.WorkflowPath,
            ActivityId = rpa007Activity.ActivityId,
            PropertyName = rpa007.PropertyName
        }, CancellationToken.None)).Suggestion!;

        var apply = await services.Applier.ApplyAsync(new UiPathFixApplyRequest
        {
            ProjectPath = copy.RootPath,
            FixSuggestionId = suggestion.Id,
            RuleId = suggestion.RuleId,
            WorkflowPath = suggestion.WorkflowPath!,
            ActivityId = suggestion.ActivityId,
            PropertyName = suggestion.PropertyName!,
            ExpectedCurrentValue = suggestion.CurrentValue,
            SuggestedValue = suggestion.SuggestedValue!,
            ExpectedFileHash = suggestion.ExpectedFileHash
        }, CancellationToken.None);
        Assert.True(apply.Success, apply.Message);

        var loginPath = Path.Combine(copy.RootPath, "Business", "Login.xaml");
        var document = XDocument.Load(loginPath, LoadOptions.PreserveWhitespace);
        var clicked = document.Descendants().First(element => element.Name.LocalName == "Click");
        clicked.SetAttributeValue("DisplayName", "Click Submit");
        document.Save(loginPath, SaveOptions.DisableFormatting);

        var backup = Assert.Single(services.BackupRepository.ListBackups(copy.RootPath).Where(item => item.WorkflowPath == "Business/Login.xaml"));
        var undo = await services.Undo.UndoAsync(new UiPathUndoRequest
        {
            ProjectPath = copy.RootPath,
            BackupId = backup.BackupId,
            WorkflowPath = backup.WorkflowPath!,
            ExpectedCurrentHash = backup.ModifiedHash
        }, CancellationToken.None);

        Assert.False(undo.Success);
        Assert.False(undo.Restored);
        Assert.Equal("UNDO_FILE_CHANGED", undo.ErrorCode);
        Assert.Contains("Click Submit", File.ReadAllText(loginPath), StringComparison.Ordinal);
    }

    [Fact]
    public void RealisticValidationProject_BackupWorkflowsAreIgnoredByScanner()
    {
        using var copy = FixtureCopy();
        Directory.CreateDirectory(Path.Combine(copy.RootPath, ".rpadevassistant", "backups", "fake", "Business"));
        File.Copy(
            Path.Combine(copy.RootPath, "Business", "Login.xaml"),
            Path.Combine(copy.RootPath, ".rpadevassistant", "backups", "fake", "Business", "Login.xaml"));

        var scan = new UiPathProjectScanner(new UiPathXamlParser()).Scan(copy.RootPath);

        Assert.DoesNotContain(scan.Workflows, workflow => workflow.RelativePath.StartsWith(".rpadevassistant/", StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateInvocationGraph(UiPathProjectAnalysisResult result)
    {
        var graph = new UiPathWorkflowGraphBuilder().Build(result.ProjectScan);

        Assert.Contains(graph.Edges, edge => edge.CallerWorkflowPath == "Main.xaml" && edge.CalleeWorkflowPath == "Framework/InitAllSettings.xaml");
        Assert.Contains(graph.Edges, edge => edge.CallerWorkflowPath == "Main.xaml" && edge.CalleeWorkflowPath == "Framework/GetTransactionData.xaml");
        Assert.Contains(graph.Edges, edge => edge.CallerWorkflowPath == "Main.xaml" && edge.CalleeWorkflowPath == "Framework/Process.xaml");
        Assert.Contains(graph.Edges, edge => edge.CallerWorkflowPath == "Framework/Process.xaml" && edge.CalleeWorkflowPath == "Business/Login.xaml");
        Assert.Contains(graph.Edges, edge => edge.CallerWorkflowPath == "Business/CallExternalApi.xaml" && edge.CalleeWorkflowPath is null && edge.RawReference.Contains("MissingCleanup", StringComparison.Ordinal));
    }

    private static async Task ValidateAskProjectAsync(IUiPathProjectQuestionService service, string projectPath, UiPathProjectAnalysisResult result)
    {
        var delay = await Ask(service, projectPath, "Delay nerede kullanılıyor?");
        Assert.False(delay.UsedAi);
        Assert.Contains("Business/Login.xaml", delay.Answer, StringComparison.Ordinal);

        var workflowCount = await Ask(service, projectPath, "Kaç workflow var?");
        Assert.False(workflowCount.UsedAi);
        Assert.Contains(result.WorkflowCount.ToString(CultureInfo.InvariantCulture), workflowCount.Answer, StringComparison.Ordinal);

        var topFinding = await Ask(service, projectPath, "En fazla finding hangi workflow'da?");
        Assert.False(topFinding.UsedAi);
        Assert.Contains("Business/Login.xaml", topFinding.Answer, StringComparison.Ordinal);

        var mainCalls = await Ask(service, projectPath, "Main.xaml hangi workflow'ları çağırıyor?");
        Assert.False(mainCalls.UsedAi);
        Assert.Contains("Framework/InitAllSettings.xaml", mainCalls.Answer, StringComparison.Ordinal);
        Assert.Contains("Framework/GetTransactionData.xaml", mainCalls.Answer, StringComparison.Ordinal);
        Assert.Contains("Framework/Process.xaml", mainCalls.Answer, StringComparison.Ordinal);

        var httpUsage = await Ask(service, projectPath, "HTTP Request nerede kullanılıyor?");
        Assert.False(httpUsage.UsedAi);
        Assert.Contains("Business/CallExternalApi.xaml", httpUsage.Answer, StringComparison.Ordinal);
        Assert.Contains("Business/GetCustomerData.xaml", httpUsage.Answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Business/SendMail.xaml", string.Join(Environment.NewLine, httpUsage.Evidence.Select(item => item.WorkflowPath)), StringComparison.Ordinal);

        var credentialUsage = await Ask(service, projectPath, "Get Credential nerede kullanılıyor?");
        Assert.False(credentialUsage.UsedAi);
        Assert.Contains("Framework/InitAllSettings.xaml", credentialUsage.Answer, StringComparison.Ordinal);
    }

    private static async Task ValidateAiReviewRedactionAsync(ValidationServices services, string projectPath)
    {
        var provider = new CapturingAiReviewProvider();
        var aiService = new UiPathAiReviewService(
            services.Analyzer,
            new UiPathAiReviewContextBuilder(new SensitiveValueRedactor()),
            new UiPathAiPromptBuilder(),
            provider,
            NullLogger<UiPathAiReviewService>.Instance);

        var projectReview = await aiService.ReviewAsync(projectPath, null, UiPathAiReviewScope.Project, null, null, CancellationToken.None);
        var workflowReview = await aiService.ReviewAsync(projectPath, null, UiPathAiReviewScope.Workflow, "Business/Login.xaml", null, CancellationToken.None);

        Assert.True(projectReview.IsSuccess);
        Assert.True(workflowReview.IsSuccess);
        Assert.DoesNotContain(SecretFixtureValue, provider.LastPrompt!.UserContext, StringComparison.Ordinal);
        Assert.DoesNotContain("<Activity", provider.LastPrompt.UserContext, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", provider.LastPrompt.UserContext, StringComparison.Ordinal);
    }

    private static void ValidateReports(UiPathAnalysisReport report, int findingCount)
    {
        var json = new JsonUiPathReportExporter().Export(report);
        var html = new HtmlUiPathReportExporter().Export(report);

        Assert.Contains("RealisticValidationProject", json.Content, StringComparison.Ordinal);
        Assert.Contains("\"qualityScore\"", json.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretFixtureValue, json.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretFixtureValue, html.Content, StringComparison.Ordinal);
        Assert.Contains("<!doctype html>", html.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RealisticValidationProject", html.Content, StringComparison.Ordinal);
        Assert.Equal(findingCount, report.Summary.TotalFindings);
        Assert.Equal(findingCount, report.Findings.Count);
    }

    private static async Task<UiPathProjectAnswer> Ask(IUiPathProjectQuestionService service, string projectPath, string question)
    {
        return await service.AskAsync(new UiPathProjectQuestion
        {
            ProjectPath = projectPath,
            Question = question,
            MaxEvidenceItems = 30
        }, CancellationToken.None);
    }

    private static ValidationMetrics CalculateMetrics(ValidationManifest manifest, IReadOnlyList<UiPathAnalysisFinding> findings)
    {
        var expectedTotal = manifest.ExpectedFindings.Sum(item => item.MinimumCount);
        var detectedExpected = manifest.ExpectedFindings.Sum(expected =>
            Math.Min(expected.MinimumCount, Find(findings, expected.RuleId, expected.WorkflowPath).Count()));
        var missed = expectedTotal - detectedExpected;

        var expectedPairs = manifest.ExpectedFindings
            .Select(item => Key(item.RuleId, item.WorkflowPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowedPairs = manifest.AllowedAdditionalFindings
            .Select(item => Key(item.RuleId, item.WorkflowPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unexpected = findings
            .Where(finding => !expectedPairs.Contains(Key(finding.RuleId, finding.WorkflowPath)) && !allowedPairs.Contains(Key(finding.RuleId, finding.WorkflowPath)))
            .ToArray();

        var truePositives = findings.Count - unexpected.Length;
        return new ValidationMetrics(
            expectedTotal,
            detectedExpected,
            missed,
            unexpected,
            truePositives / (double)Math.Max(1, findings.Count),
            detectedExpected / (double)Math.Max(1, expectedTotal));
    }

    private static IEnumerable<UiPathAnalysisFinding> Find(IEnumerable<UiPathAnalysisFinding> findings, string ruleId, string? workflowPath)
    {
        return findings.Where(finding =>
            finding.RuleId.Equals(ruleId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finding.WorkflowPath, workflowPath, StringComparison.OrdinalIgnoreCase));
    }

    private static string Describe(UiPathAnalysisFinding finding)
    {
        return $"{finding.RuleId}@{finding.WorkflowPath}";
    }

    private static string Key(string ruleId, string? workflowPath)
    {
        return $"{ruleId}|{workflowPath ?? "Project"}";
    }

    private static void WriteValidationArtifacts(string projectPath, UiPathProjectAnalysisResult result, ValidationMetrics metrics)
    {
        var artifactDir = Path.Combine(RepositoryRoot(), "artifacts", "validation");
        Directory.CreateDirectory(artifactDir);

        var ruleRows = result.Analysis.Findings
            .GroupBy(finding => finding.RuleId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Rule = group.Key, Actual = group.Count() })
            .ToArray();
        var workflowRows = result.ProjectScan.Workflows
            .OrderBy(workflow => workflow.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(workflow => new
            {
                Workflow = workflow.RelativePath,
                Activities = workflow.ActivityCount,
                Findings = result.Analysis.Findings.Count(finding => finding.WorkflowPath == workflow.RelativePath)
            })
            .ToArray();

        var payload = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            productVersion = ProductInfo.ProductVersion,
            projectFixture = projectPath,
            result.WorkflowCount,
            result.TotalActivityCount,
            expectedFindings = metrics.ExpectedFindings,
            truePositives = metrics.DetectedExpectedFindings,
            falsePositives = metrics.UnexpectedFindings.Count,
            falseNegatives = metrics.MissedExpectedFindings,
            precision = metrics.Precision,
            recall = metrics.Recall,
            qualityScore = result.QualityScore.Score,
            grade = result.QualityScore.Grade,
            ruleRows,
            workflowRows,
            knownIssues = Array.Empty<string>()
        };

        File.WriteAllText(
            Path.Combine(artifactDir, "real-project-validation.json"),
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        var markdown = new StringBuilder();
        markdown.AppendLine("# Real UiPath Project Validation");
        markdown.AppendLine();
        markdown.AppendLine($"Generated: {DateTimeOffset.UtcNow:O}");
        markdown.AppendLine($"Fixture: `{projectPath}`");
        markdown.AppendLine($"Workflows: {result.WorkflowCount}");
        markdown.AppendLine($"Activities: {result.TotalActivityCount}");
        markdown.AppendLine($"Expected findings: {metrics.ExpectedFindings}");
        markdown.AppendLine($"Detected expected findings: {metrics.DetectedExpectedFindings}");
        markdown.AppendLine($"Missed expected findings: {metrics.MissedExpectedFindings}");
        markdown.AppendLine($"Unexpected findings: {metrics.UnexpectedFindings.Count}");
        markdown.AppendLine($"Precision: {metrics.Precision:P2}");
        markdown.AppendLine($"Recall: {metrics.Recall:P2}");
        markdown.AppendLine($"Quality score: {result.QualityScore.Score} / {result.QualityScore.Grade}");
        markdown.AppendLine();
        markdown.AppendLine("| Rule | Actual |");
        markdown.AppendLine("| --- | ---: |");
        foreach (var row in ruleRows)
        {
            markdown.AppendLine($"| {row.Rule} | {row.Actual} |");
        }

        File.WriteAllText(Path.Combine(artifactDir, "real-project-validation.md"), markdown.ToString());
    }

    private static ValidationServices CreateServices()
    {
        var parser = new UiPathXamlParser();
        var scanner = new UiPathProjectScanner(parser);
        var expressionClassifier = new UiPathExpressionClassifier();
        var selectorAnalyzer = new UiPathSelectorAnalyzer();
        var metricsCalculator = new UiPathWorkflowMetricsCalculator();
        var analyzer = new UiPathProjectAnalyzer(
            scanner,
            new UiPathRuleEngine(AllRules(expressionClassifier, selectorAnalyzer, metricsCalculator)),
            new BuiltInUiPathRuleProfileProvider(),
            new UiPathQualityScoringEngine());
        var graphBuilder = new UiPathWorkflowGraphBuilder();
        var suggestions = new UiPathFixSuggestionService(
            analyzer,
            new UiPathFixSuggestionRegistry([
                new DelayFixSuggestionProvider(),
                new WorkflowNamingFixSuggestionProvider(),
                new DisplayNameFixSuggestionProvider(),
                new MissingLoggingFixSuggestionProvider(),
                new ExceptionHandlingFixSuggestionProvider(),
                new InvalidInvokeWorkflowFixSuggestionProvider(),
                new ExpandedRuleManualFixSuggestionProvider()
            ]),
            new UiPathFixContextBuilder(graphBuilder),
            new UiPathFixSuggestionValidator(),
            new UiPathAiFixPromptBuilder(new SensitiveValueRedactor()),
            new FakeAiFixAdvisor(),
            NullLogger<UiPathFixSuggestionService>.Instance);
        var backupRepository = new UiPathBackupRepository(new UiPathUndoEligibilityService());
        var mutationLock = new UiPathMutationLock();
        return new ValidationServices(
            analyzer,
            new UiPathAnalysisReportBuilder(),
            new UiPathProjectQuestionService(
                analyzer,
                new UiPathProjectQuestionClassifier(),
                new UiPathProjectRetriever(new SensitiveValueRedactor()),
                graphBuilder,
                new UiPathProjectAssistantPromptBuilder(),
                new FakeProjectAssistantAiProvider(),
                NullLogger<UiPathProjectQuestionService>.Instance),
            suggestions,
            new UiPathFixApplier(
                suggestions,
                analyzer,
                new UiPathMutationPolicy(),
                new UiPathXamlMutationService(),
                new UiPathBackupService(),
                new UiPathMutationAuditLogger(),
                parser,
                scanner,
                mutationLock),
            backupRepository,
            new UiPathUndoService(
                backupRepository,
                new UiPathBackupRestoreService(backupRepository, parser, scanner, new UiPathRestoreAuditLogger()),
                mutationLock));
    }

    private static IUiPathAnalysisRule[] AllRules(
        IUiPathExpressionClassifier expressionClassifier,
        IUiPathSelectorAnalyzer selectorAnalyzer,
        IUiPathWorkflowMetricsCalculator metricsCalculator)
    {
        return
        [
            new AvoidDelayActivitiesRule(),
            new EmptyCatchBlockRule(),
            new ExceptionSilentlySwallowedRule(),
            new LongHardCodedDelayRule(),
            new InvalidInvokeWorkflowReferenceRule(),
            new WorkflowNamingConventionRule(),
            new GenericActivityDisplayNameRule(),
            new WorkflowHasNoLoggingRule(),
            new HardCodedCredentialLikeValueRule(expressionClassifier),
            new SensitiveValueInLogMessageRule(),
            new ExcessiveUiTimeoutRule(expressionClassifier),
            new UnrealisticallyLowUiTimeoutRule(expressionClassifier),
            new ContinueOnErrorEnabledRule(),
            new ExcessiveContinueOnErrorUsageRule(metricsCalculator),
            new MissingExplicitTimeoutOnCriticalUiActivityRule(),
            new LegacyUiAutomationActivityRule(),
            new SelectorUsesIdxAttributeRule(selectorAnalyzer),
            new PotentiallyUnstableSelectorAttributeRule(selectorAnalyzer),
            new OverlyComplexSelectorRule(selectorAnalyzer),
            new HardCodedAbsoluteFilePathRule(expressionClassifier),
            new HardCodedEmailAddressRule(expressionClassifier),
            new HttpRequestWithoutExplicitTimeoutRule(expressionClassifier),
            new HttpRequestWithoutLocalErrorHandlingRule(),
            new ExcessiveWorkflowArgumentsRule(metricsCalculator),
            new LargeWorkflowRule(metricsCalculator)
        ];
    }

    private static ValidationManifest LoadManifest(string projectPath)
    {
        return JsonSerializer.Deserialize<ValidationManifest>(
            File.ReadAllText(Path.Combine(projectPath, "validation-manifest.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static string FixturePath()
    {
        return Path.Combine(RepositoryRoot(), "samples", "RealisticValidationProject");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RpaDevAssistant.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root could not be located.");
    }

    private static FixtureProjectCopy FixtureCopy()
    {
        var target = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantRealValidation-{Guid.NewGuid():N}");
        CopyDirectory(FixturePath(), target);
        return new FixtureProjectCopy(target);
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }
    }

    private sealed record ValidationServices(
        IUiPathProjectAnalyzer Analyzer,
        IUiPathAnalysisReportBuilder ReportBuilder,
        IUiPathProjectQuestionService QuestionService,
        IUiPathFixSuggestionService FixSuggestions,
        IUiPathFixApplier Applier,
        IUiPathBackupRepository BackupRepository,
        IUiPathUndoService Undo);

    private sealed record ValidationMetrics(
        int ExpectedFindings,
        int DetectedExpectedFindings,
        int MissedExpectedFindings,
        IReadOnlyList<UiPathAnalysisFinding> UnexpectedFindings,
        double Precision,
        double Recall);

    private sealed record ValidationManifest
    {
        public ScoreRange ExpectedScoreRange { get; init; } = new();

        public IReadOnlyList<ExpectedFinding> ExpectedFindings { get; init; } = [];

        public IReadOnlyList<ExpectedNoFinding> ExpectedNoFindings { get; init; } = [];

        public IReadOnlyList<AllowedAdditionalFinding> AllowedAdditionalFindings { get; init; } = [];
    }

    private sealed record ScoreRange
    {
        public int Min { get; init; }

        public int Max { get; init; }
    }

    private sealed record ExpectedFinding
    {
        public required string RuleId { get; init; }

        public required string WorkflowPath { get; init; }

        public int MinimumCount { get; init; } = 1;

        public string? Severity { get; init; }

        public string? ExpectedCurrentValue { get; init; }
    }

    private sealed record ExpectedNoFinding
    {
        public required string RuleId { get; init; }

        public required string WorkflowPath { get; init; }

        public string? Reason { get; init; }
    }

    private sealed record AllowedAdditionalFinding
    {
        public required string RuleId { get; init; }

        public required string WorkflowPath { get; init; }

        public string? Reason { get; init; }
    }

    private sealed class FixtureProjectCopy : IDisposable
    {
        public FixtureProjectCopy(string rootPath)
        {
            RootPath = rootPath;
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

    private sealed class FakeAiFixAdvisor : IUiPathAiFixAdvisor
    {
        public string ProviderName => "Fake";

        public bool IsConfigured => false;

        public Task<UiPathFixSuggestion> SuggestAsync(UiPathAiFixPrompt prompt, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeProjectAssistantAiProvider : IUiPathProjectAssistantAiProvider
    {
        public string ProviderName => "Fake";

        public bool IsConfigured => false;

        public Task<UiPathProjectAnswer> AnswerAsync(UiPathProjectAssistantPrompt prompt, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class CapturingAiReviewProvider : IUiPathAiReviewProvider
    {
        public UiPathAiPrompt? LastPrompt { get; private set; }

        public string ProviderName => "Capturing";

        public bool IsConfigured => true;

        public Task<UiPathAiReviewResult> ReviewAsync(UiPathAiPrompt prompt, CancellationToken cancellationToken)
        {
            LastPrompt = prompt;
            return Task.FromResult(new UiPathAiReviewResult
            {
                Summary = "Captured validation prompt.",
                RiskLevel = UiPathAiRiskLevel.Medium,
                Confidence = 0.8
            });
        }
    }
}

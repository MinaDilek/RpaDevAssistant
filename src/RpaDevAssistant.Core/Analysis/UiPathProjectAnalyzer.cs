using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Analysis.CustomRules;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Scanning;
using System.Diagnostics;

namespace RpaDevAssistant.Core.Analysis;

public sealed class UiPathProjectAnalyzer : IUiPathProjectAnalyzer
{
    private readonly IUiPathProjectScanner scanner;
    private readonly IUiPathRuleEngine ruleEngine;
    private readonly IUiPathRuleProfileProvider profileProvider;
    private readonly IUiPathQualityScoringEngine scoringEngine;
    private readonly IUiPathWorkflowMetricsCalculator metricsCalculator;
    private readonly IUiPathCustomRuleRepository? customRuleRepository;
    private readonly IUiPathCustomRuleEvaluator? customRuleEvaluator;

    public UiPathProjectAnalyzer(
        IUiPathProjectScanner scanner,
        IUiPathRuleEngine ruleEngine,
        IUiPathRuleProfileProvider profileProvider,
        IUiPathQualityScoringEngine scoringEngine,
        IUiPathWorkflowMetricsCalculator? metricsCalculator = null,
        IUiPathCustomRuleRepository? customRuleRepository = null,
        IUiPathCustomRuleEvaluator? customRuleEvaluator = null)
    {
        this.scanner = scanner;
        this.ruleEngine = ruleEngine;
        this.profileProvider = profileProvider;
        this.scoringEngine = scoringEngine;
        this.metricsCalculator = metricsCalculator ?? new UiPathWorkflowMetricsCalculator();
        this.customRuleRepository = customRuleRepository;
        this.customRuleEvaluator = customRuleEvaluator;
    }

    public UiPathProjectAnalysisResult Analyze(string projectPath, string? profileId = null)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var profile = profileProvider.GetProfile(profileId);
        var projectScan = scanner.Scan(projectPath);
        return AnalyzeScan(projectScan, profile, totalStopwatch);
    }

    public async Task<UiPathProjectAnalysisResult> AnalyzeAsync(
        string projectPath,
        string? profileId = null,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var profile = profileProvider.GetProfile(profileId);
        var projectScan = await scanner.ScanAsync(projectPath, cancellationToken).ConfigureAwait(false);
        return AnalyzeScan(projectScan, profile, totalStopwatch);
    }

    private UiPathProjectAnalysisResult AnalyzeScan(
        ProjectScanResult projectScan,
        UiPathRuleProfile profile,
        Stopwatch totalStopwatch)
    {
        var context = new UiPathAnalysisContext
        {
            Project = projectScan
        };
        var ruleStopwatch = Stopwatch.StartNew();
        var analysis = ruleEngine.Analyze(context, profile);
        AddCustomRuleFindings(context, analysis, profile);
        UpdateWorkflowComplexity(projectScan, analysis);
        ruleStopwatch.Stop();

        var scoringStopwatch = Stopwatch.StartNew();
        var qualityScore = scoringEngine.Calculate(projectScan, analysis, profile);
        scoringStopwatch.Stop();
        totalStopwatch.Stop();

        return new UiPathProjectAnalysisResult
        {
            ProjectScan = projectScan,
            Analysis = analysis,
            QualityScore = qualityScore,
            Profile = profile,
            Performance = new UiPathAnalysisPerformanceMetrics
            {
                TotalElapsedMilliseconds = totalStopwatch.Elapsed.TotalMilliseconds,
                ScanElapsedMilliseconds = projectScan.Performance.TotalElapsedMilliseconds,
                WorkflowDiscoveryElapsedMilliseconds = projectScan.Performance.WorkflowDiscoveryElapsedMilliseconds,
                XamlParsingElapsedMilliseconds = projectScan.Performance.XamlParsingElapsedMilliseconds,
                RuleAnalysisElapsedMilliseconds = ruleStopwatch.Elapsed.TotalMilliseconds,
                ScoringElapsedMilliseconds = scoringStopwatch.Elapsed.TotalMilliseconds
            }
        };
    }

    private void AddCustomRuleFindings(UiPathAnalysisContext context, UiPathStaticAnalysisResult analysis, UiPathRuleProfile profile)
    {
        if (customRuleRepository is null || customRuleEvaluator is null)
        {
            return;
        }

        var customRules = customRuleRepository.GetRules().Where(rule => !rule.IsTemplate).ToArray();
        if (customRules.Length == 0)
        {
            return;
        }

        var configurations = profile.Rules.ToDictionary(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase);
        var effectiveRules = customRules
            .Select(rule =>
            {
                if (!configurations.TryGetValue(rule.Id, out var configuration))
                {
                    return rule;
                }

                return rule with
                {
                    Enabled = configuration.Enabled,
                    Severity = configuration.SeverityOverride ?? rule.Severity,
                    Weight = configuration.Weight,
                    MaxPenalty = configuration.MaxPenalty
                };
            })
            .Where(rule => rule.Enabled)
            .ToArray();

        analysis.Findings.AddRange(customRuleEvaluator.Analyze(context, effectiveRules));
    }

    private void UpdateWorkflowComplexity(ProjectScanResult projectScan, UiPathStaticAnalysisResult analysis)
    {
        var findingsByWorkflow = analysis.Findings
            .Where(finding => !string.IsNullOrWhiteSpace(finding.WorkflowPath))
            .GroupBy(finding => finding.WorkflowPath!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var workflow in projectScan.Workflows)
        {
            if (workflow.Analysis is null)
            {
                continue;
            }

            workflow.Analysis.Complexity = metricsCalculator.CalculateComplexity(
                workflow.Analysis,
                findingsByWorkflow.GetValueOrDefault(workflow.RelativePath));
        }
    }
}

using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Analysis.CustomRules;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Scanning;

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
        var profile = profileProvider.GetProfile(profileId);
        var projectScan = scanner.Scan(projectPath);
        var context = new UiPathAnalysisContext
        {
            Project = projectScan
        };
        var analysis = ruleEngine.Analyze(context, profile);
        AddCustomRuleFindings(context, analysis, profile);
        UpdateWorkflowComplexity(projectScan, analysis);
        var qualityScore = scoringEngine.Calculate(projectScan, analysis, profile);

        return new UiPathProjectAnalysisResult
        {
            ProjectScan = projectScan,
            Analysis = analysis,
            QualityScore = qualityScore,
            Profile = profile
        };
    }

    private void AddCustomRuleFindings(UiPathAnalysisContext context, UiPathStaticAnalysisResult analysis, UiPathRuleProfile profile)
    {
        if (customRuleRepository is null || customRuleEvaluator is null)
        {
            return;
        }

        var customRules = customRuleRepository.GetRules();
        if (customRules.Count == 0)
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

using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis.Scoring;

public sealed class UiPathQualityScoringEngine : IUiPathQualityScoringEngine
{
    public UiPathQualityScore Calculate(ProjectScanResult project, UiPathStaticAnalysisResult analysis, UiPathRuleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(profile);

        var breakdown = BuildScoreBreakdown(analysis, profile);
        var rawPenalty = breakdown.Sum(item => item.AppliedPenalty);
        var projectSizeFactor = CalculateProjectSizeFactor(project.WorkflowCount, project.TotalActivityCount);
        var normalizedPenalty = NormalizePenalty(rawPenalty, project.WorkflowCount, project.TotalActivityCount);
        var score = CalculateFinalScore(normalizedPenalty);

        return new UiPathQualityScore
        {
            Score = score,
            Grade = CalculateGrade(score),
            RawPenalty = Math.Round(rawPenalty, 2),
            NormalizedPenalty = Math.Round(normalizedPenalty, 2),
            ProjectSizeFactor = Math.Round(projectSizeFactor, 2),
            TotalFindings = analysis.TotalFindings,
            ProfileId = profile.Id,
            ProfileName = profile.Name,
            ScoreBreakdown = breakdown,
            SeverityBreakdown = BuildSeverityBreakdown(breakdown)
        };
    }

    public static double GetSeverityMultiplier(RuleSeverity severity)
    {
        return severity switch
        {
            RuleSeverity.Info => 0,
            RuleSeverity.Suggestion => 0.5,
            RuleSeverity.Warning => 1,
            RuleSeverity.Error => 1.5,
            RuleSeverity.Critical => 2,
            _ => 1
        };
    }

    public static RuleSeverity GetEffectiveSeverity(UiPathAnalysisFinding finding, UiPathRuleConfiguration configuration)
    {
        return configuration.SeverityOverride ?? finding.Severity;
    }

    public static double CalculateRuleRawPenalty(int findingCount, double weight, RuleSeverity severity)
    {
        return findingCount * weight * GetSeverityMultiplier(severity);
    }

    public static double ApplyMaxPenalty(double rawPenalty, double maxPenalty)
    {
        return Math.Min(rawPenalty, maxPenalty);
    }

    public static double CalculateProjectSizeFactor(int workflowCount, int totalActivityCount)
    {
        var workflowComponent = Math.Max(1, workflowCount);
        var activityComponent = Math.Max(0, totalActivityCount) / 10.0;
        var sizeUnits = Math.Max(1, workflowComponent + activityComponent);
        return Math.Clamp(Math.Sqrt(sizeUnits), 1, 5);
    }

    public static double NormalizePenalty(double rawPenalty, int workflowCount, int totalActivityCount)
    {
        return rawPenalty / CalculateProjectSizeFactor(workflowCount, totalActivityCount);
    }

    public static int CalculateFinalScore(double normalizedPenalty)
    {
        return Math.Clamp(100 - (int)Math.Round(normalizedPenalty, MidpointRounding.AwayFromZero), 0, 100);
    }

    public static string CalculateGrade(int score)
    {
        return score switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            >= 60 => "D",
            _ => "F"
        };
    }

    private static IReadOnlyList<UiPathRuleScoreBreakdown> BuildScoreBreakdown(UiPathStaticAnalysisResult analysis, UiPathRuleProfile profile)
    {
        var enabledConfigurations = profile.Rules
            .Where(rule => rule.Enabled)
            .ToDictionary(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase);

        return analysis.Findings
            .Where(finding => enabledConfigurations.ContainsKey(finding.RuleId))
            .GroupBy(finding => finding.RuleId, StringComparer.OrdinalIgnoreCase)
            .Select(group => CreateBreakdown(group, enabledConfigurations[group.Key]))
            .OrderBy(item => item.RuleId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<UiPathSeverityScoreBreakdown> BuildSeverityBreakdown(IReadOnlyList<UiPathRuleScoreBreakdown> breakdown)
    {
        return breakdown
            .GroupBy(item => item.Severity)
            .Select(group => new UiPathSeverityScoreBreakdown
            {
                Severity = group.Key,
                FindingCount = group.Sum(item => item.FindingCount),
                OccurrenceCount = group.Sum(item => item.OccurrenceCount),
                RawPenalty = Math.Round(group.Sum(item => item.RawPenalty), 2),
                AppliedPenalty = Math.Round(group.Sum(item => item.AppliedPenalty), 2)
            })
            .OrderBy(item => item.Severity)
            .ToArray();
    }

    private static UiPathRuleScoreBreakdown CreateBreakdown(IGrouping<string, UiPathAnalysisFinding> findingGroup, UiPathRuleConfiguration configuration)
    {
        var firstFinding = findingGroup.First();
        var effectiveSeverity = GetEffectiveSeverity(firstFinding, configuration);
        var findingCount = findingGroup.Count();
        var occurrenceCount = findingGroup.Sum(finding => Math.Max(1, finding.OccurrenceCount));
        var rawPenalty = CalculateRuleRawPenalty(findingCount, configuration.Weight, effectiveSeverity);
        var appliedPenalty = ApplyMaxPenalty(rawPenalty, configuration.MaxPenalty);

        return new UiPathRuleScoreBreakdown
        {
            RuleId = firstFinding.RuleId,
            RuleName = firstFinding.RuleName,
            FindingCount = findingCount,
            OccurrenceCount = occurrenceCount,
            Severity = effectiveSeverity,
            Weight = configuration.Weight,
            RawPenalty = Math.Round(rawPenalty, 2),
            AppliedPenalty = Math.Round(appliedPenalty, 2),
            MaxPenalty = configuration.MaxPenalty
        };
    }
}

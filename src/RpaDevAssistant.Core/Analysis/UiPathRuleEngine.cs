using RpaDevAssistant.Core.Analysis.Profiles;

namespace RpaDevAssistant.Core.Analysis;

public sealed class UiPathRuleEngine : IUiPathRuleEngine
{
    private readonly IReadOnlyCollection<IUiPathAnalysisRule> rules;

    public UiPathRuleEngine(IEnumerable<IUiPathAnalysisRule> rules)
    {
        this.rules = rules.ToArray();
    }

    public UiPathStaticAnalysisResult Analyze(UiPathAnalysisContext context, UiPathRuleProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = new UiPathStaticAnalysisResult();
        var enabledConfigurations = profile?.Rules
            .Where(configuration => configuration.Enabled)
            .ToDictionary(configuration => configuration.RuleId, StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules.OrderBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (enabledConfigurations is not null && !enabledConfigurations.ContainsKey(rule.Id))
            {
                continue;
            }

            try
            {
                UiPathRuleConfiguration? configuration = null;
                enabledConfigurations?.TryGetValue(rule.Id, out configuration);
                var findings = rule.Analyze(context with { RuleConfiguration = configuration });
                if (configuration is not null)
                {
                    findings = findings.Select(finding => ApplyConfiguration(finding, configuration));
                }

                result.Findings.AddRange(findings);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                var severity = RuleSeverity.Error;
                if (enabledConfigurations is not null
                    && enabledConfigurations.TryGetValue(rule.Id, out var configuration)
                    && configuration.SeverityOverride is not null)
                {
                    severity = configuration.SeverityOverride.Value;
                }

                result.Findings.Add(new UiPathAnalysisFinding
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Severity = severity,
                    Category = rule.Category,
                    Message = $"Rule {rule.Id} failed during analysis.",
                    Description = ex.Message,
                    Recommendation = "Inspect the rule implementation and rerun the analysis."
                });
            }
        }

        return result;
    }

    private static UiPathAnalysisFinding ApplyConfiguration(UiPathAnalysisFinding finding, UiPathRuleConfiguration configuration)
    {
        return configuration.SeverityOverride is null
            ? finding
            : finding with { Severity = configuration.SeverityOverride.Value };
    }
}

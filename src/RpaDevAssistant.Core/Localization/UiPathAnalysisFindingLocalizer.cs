using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.Localization;

public sealed class UiPathAnalysisFindingLocalizer
{
    private readonly IRpaDevAssistantLocalizer localizer;

    public UiPathAnalysisFindingLocalizer(IRpaDevAssistantLocalizer localizer)
    {
        this.localizer = localizer;
    }

    public UiPathStaticAnalysisResult Localize(UiPathStaticAnalysisResult result, string? locale)
    {
        var localized = new UiPathStaticAnalysisResult();
        localized.Findings.AddRange(result.Findings.Select(finding => Localize(finding, locale)));
        return localized;
    }

    public UiPathAnalysisFinding Localize(UiPathAnalysisFinding finding, string? locale)
    {
        if (finding.Source.Equals("Custom", StringComparison.OrdinalIgnoreCase))
        {
            return finding with
            {
                RuleName = LocalizeCustomText(finding.RuleName, locale) ?? finding.RuleName,
                Message = LocalizeCustomText(finding.Message, locale) ?? finding.Message,
                Description = LocalizeCustomText(finding.Description, locale) ?? string.Empty,
                Recommendation = LocalizeCustomText(finding.Recommendation, locale) ?? string.Empty
            };
        }

        return finding with
        {
            RuleName = localizer.Get($"Rules.{finding.RuleId}.Name", locale, fallback: finding.RuleName),
            Message = localizer.Get($"Rules.{finding.RuleId}.Message", locale, fallback: finding.Message),
            Description = localizer.Get($"Rules.{finding.RuleId}.Description", locale, fallback: finding.Description ?? string.Empty),
            Recommendation = localizer.Get($"Rules.{finding.RuleId}.Recommendation", locale, fallback: finding.Recommendation ?? string.Empty)
        };
    }

    private static string? LocalizeCustomText(string? packedText, string? locale)
    {
        if (string.IsNullOrWhiteSpace(packedText))
        {
            return packedText;
        }

        var parts = packedText.Split('\u001f');
        if (parts.Length != 3)
        {
            return packedText;
        }

        var fallback = parts[0];
        var english = parts[1];
        var turkish = parts[2];
        if (string.Equals(locale, "tr", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(turkish))
        {
            return turkish;
        }

        if (string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(english))
        {
            return english;
        }

        return !string.IsNullOrWhiteSpace(english) ? english : fallback;
    }
}

using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Localization;
using RpaDevAssistant.Core.Analysis.RuleCatalog;

namespace RpaDevAssistant.Core.Analysis;

public sealed class UiPathRuleCatalogProvider : IUiPathRuleCatalogProvider
{
    private readonly IUiPathRuleCatalog catalog;
    private readonly IUiPathMutationPolicy mutationPolicy;
    private readonly IRpaDevAssistantLocalizer localizer;

    public UiPathRuleCatalogProvider(
        IUiPathRuleCatalog catalog,
        IUiPathMutationPolicy mutationPolicy,
        IRpaDevAssistantLocalizer? localizer = null)
    {
        this.catalog = catalog;
        this.mutationPolicy = mutationPolicy;
        this.localizer = localizer ?? new RpaDevAssistantLocalizer();
    }

    public UiPathRuleCatalogProvider(
        IEnumerable<IUiPathAnalysisRule> rules,
        IEnumerable<IUiPathFixSuggestionProvider> fixProviders,
        IUiPathMutationPolicy mutationPolicy,
        IRpaDevAssistantLocalizer? localizer = null)
        : this(
            new RuleCatalog.UiPathRuleCatalog(
                rules,
                new Profiles.BuiltInUiPathRuleProfileProvider(),
                new CustomRules.InMemoryUiPathCustomRuleRepository(),
                fixProviders),
            mutationPolicy,
            localizer)
    {
    }

    public IReadOnlyList<UiPathRuleCatalogItem> GetRules(string? locale = null)
    {
        return catalog.GetAllRules()
            .OrderBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .Select(rule => new UiPathRuleCatalogItem
            {
                Id = rule.Id,
                Name = Localize(rule, rule.NameKey, locale, rule.CustomName ?? rule.NameKey),
                Description = Localize(rule, rule.DescriptionKey, locale, rule.CustomDescription ?? string.Empty),
                Category = rule.Category,
                DefaultSeverity = rule.DefaultSeverity,
                Recommendation = Localize(rule, rule.RecommendationKey, locale, rule.CustomRecommendation ?? string.Empty),
                HasFixSuggestion = rule.SupportsFixSuggestion,
                CanAutoApply = mutationPolicy.CanAutoApplyRule(rule.Id),
                Scope = rule.Scope,
                EnabledByDefault = rule.EnabledByDefault,
                IsBuiltIn = rule.IsBuiltIn,
                IsTemplate = rule.IsTemplate,
                TemplateId = rule.TemplateId,
                TemplateSource = rule.TemplateSource,
                SupportsAggregation = rule.SupportsAggregation,
                DefaultWeight = rule.DefaultWeight,
                DefaultMaxPenalty = rule.DefaultMaxPenalty,
                Tags = rule.Tags,
                ApplicableProjectTypes = rule.ApplicableProjectTypes,
                CompatibilityNotes = localizer.Get(
                    rule.CompatibilityNotesKey ?? "RuleCatalog.AllProjectTypes",
                    locale,
                    fallback: string.Empty)
            })
            .ToArray();
    }

    private string Localize(UiPathRuleDefinition rule, string key, string? locale, string fallback)
    {
        return rule.IsBuiltIn ? localizer.Get(key, locale, fallback: fallback) : LocalizeCustomText(fallback, locale);
    }

    private static string LocalizeCustomText(string packedText, string? locale)
    {
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

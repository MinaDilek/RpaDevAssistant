using RpaDevAssistant.Core.Analysis.CustomRules;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Fixes;

namespace RpaDevAssistant.Core.Analysis.RuleCatalog;

public sealed class UiPathRuleCatalog : IUiPathRuleCatalog
{
    private readonly IReadOnlyCollection<IUiPathAnalysisRule> builtInRules;
    private readonly IUiPathRuleProfileProvider profileProvider;
    private readonly IUiPathCustomRuleRepository customRuleRepository;
    private readonly IReadOnlyCollection<IUiPathFixSuggestionProvider> fixProviders;

    public UiPathRuleCatalog(
        IEnumerable<IUiPathAnalysisRule> builtInRules,
        IUiPathRuleProfileProvider profileProvider,
        IUiPathCustomRuleRepository customRuleRepository,
        IEnumerable<IUiPathFixSuggestionProvider> fixProviders)
    {
        this.builtInRules = builtInRules.ToArray();
        this.profileProvider = profileProvider;
        this.customRuleRepository = customRuleRepository;
        this.fixProviders = fixProviders.ToArray();
    }

    public IReadOnlyList<UiPathRuleDefinition> GetAllRules()
    {
        var profile = profileProvider.GetProfile(BuiltInUiPathRuleProfileProvider.DefaultProfileId);
        var configurations = profile.Rules.ToDictionary(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase);

        var builtIns = builtInRules
            .OrderBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .Select(rule => ToBuiltInDefinition(rule, configurations.GetValueOrDefault(rule.Id)));

        var custom = customRuleRepository.GetRules()
            .Where(rule => !rule.IsTemplate)
            .OrderBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .Select(ToCustomDefinition);

        return builtIns.Concat(custom).ToArray();
    }

    public UiPathRuleDefinition? GetRuleById(string id)
    {
        return GetAllRules().FirstOrDefault(rule => rule.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<UiPathRuleDefinition> GetRulesByCategory(RuleCategory category)
    {
        return GetAllRules().Where(rule => rule.Category == category).ToArray();
    }

    public IReadOnlyList<UiPathRuleDefinition> GetRulesByScope(UiPathRuleScope scope)
    {
        return GetAllRules().Where(rule => rule.Scope == scope).ToArray();
    }

    private UiPathRuleDefinition ToBuiltInDefinition(IUiPathAnalysisRule rule, UiPathRuleConfiguration? configuration)
    {
        return new UiPathRuleDefinition
        {
            Id = rule.Id,
            NameKey = $"Rules.{rule.Id}.Name",
            DescriptionKey = $"Rules.{rule.Id}.Description",
            RecommendationKey = $"Rules.{rule.Id}.Recommendation",
            Category = rule.Category,
            DefaultSeverity = configuration?.SeverityOverride ?? rule.Severity,
            DefaultWeight = configuration?.Weight ?? 0,
            DefaultMaxPenalty = configuration?.MaxPenalty ?? 0,
            Scope = InferScope(rule.Id),
            EnabledByDefault = configuration?.Enabled ?? true,
            IsBuiltIn = true,
            SupportsFixSuggestion = fixProviders.Any(provider => provider.SupportedRuleIds.Contains(rule.Id, StringComparer.OrdinalIgnoreCase)),
            SupportsAggregation = string.Equals(rule.Id, "RPA007", StringComparison.OrdinalIgnoreCase),
            Tags = [rule.Category.ToString(), rule.Severity.ToString()],
            ApplicableProjectTypes = ApplicableProjectTypes(rule.Id),
            CompatibilityNotesKey = string.Equals(rule.Id, "RPA016", StringComparison.OrdinalIgnoreCase)
                ? "RuleCatalog.RPA016Compatibility"
                : "RuleCatalog.AllProjectTypes",
        };
    }

    private static UiPathRuleDefinition ToCustomDefinition(UiPathCustomRuleDefinition rule)
    {
        return new UiPathRuleDefinition
        {
            Id = rule.Id,
            NameKey = CustomText(rule.Name, rule.NameEn, rule.NameTr),
            DescriptionKey = CustomText(rule.Description ?? string.Empty, rule.DescriptionEn, rule.DescriptionTr),
            RecommendationKey = CustomText(rule.Recommendation ?? string.Empty, rule.RecommendationEn, rule.RecommendationTr),
            Category = rule.Category,
            DefaultSeverity = rule.Severity,
            DefaultWeight = rule.Weight,
            DefaultMaxPenalty = rule.MaxPenalty,
            Scope = rule.Scope,
            EnabledByDefault = rule.Enabled,
            IsBuiltIn = false,
            IsTemplate = rule.IsTemplate,
            TemplateId = rule.TemplateId,
            TemplateSource = rule.TemplateSource,
            SupportsFixSuggestion = false,
            SupportsAggregation = false,
            Tags = ["Custom", rule.Category.ToString()],
            ApplicableProjectTypes = ["Windows", "Windows-Legacy", "Modern", "Classic"],
            CompatibilityNotesKey = "RuleCatalog.AllProjectTypes",
            CustomName = CustomText(rule.Name, rule.NameEn, rule.NameTr),
            CustomDescription = CustomText(rule.Description ?? string.Empty, rule.DescriptionEn, rule.DescriptionTr),
            CustomRecommendation = CustomText(rule.Recommendation ?? string.Empty, rule.RecommendationEn, rule.RecommendationTr)
        };
    }

    private static string CustomText(string fallback, string? en, string? tr)
    {
        return string.Join('\u001f', fallback, en ?? string.Empty, tr ?? string.Empty);
    }

    private static IReadOnlyList<string> ApplicableProjectTypes(string ruleId)
    {
        return string.Equals(ruleId, "RPA016", StringComparison.OrdinalIgnoreCase)
            ? ["Windows", "Modern"]
            : ["Windows", "Windows-Legacy", "Modern", "Classic"];
    }

    private static UiPathRuleScope InferScope(string ruleId)
    {
        return ruleId switch
        {
            "RPA006" or "RPA008" or "RPA014" or "RPA024" or "RPA025" or "RPA031" or "RPA032" or "RPA033" or "RPA034" or "RPA038" or "RPA039" or "RPA040" or "RPA041" or "RPA042" or "RPA046" => UiPathRuleScope.Workflow,
            _ => UiPathRuleScope.Activity
        };
    }
}

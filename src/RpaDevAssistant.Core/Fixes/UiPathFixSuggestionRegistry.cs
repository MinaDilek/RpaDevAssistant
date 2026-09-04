namespace RpaDevAssistant.Core.Fixes;

public sealed class UiPathFixSuggestionRegistry : IUiPathFixSuggestionRegistry
{
    private readonly IReadOnlyDictionary<string, IUiPathFixSuggestionProvider> providersByRuleId;

    public UiPathFixSuggestionRegistry(IEnumerable<IUiPathFixSuggestionProvider> providers)
    {
        providersByRuleId = providers
            .SelectMany(provider => provider.SupportedRuleIds.Select(ruleId => new { ruleId, provider }))
            .GroupBy(item => item.ruleId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().provider, StringComparer.OrdinalIgnoreCase);
    }

    public IUiPathFixSuggestionProvider? FindProvider(string ruleId)
    {
        return providersByRuleId.TryGetValue(ruleId, out var provider) ? provider : null;
    }

    public bool HasProvider(string ruleId)
    {
        return providersByRuleId.ContainsKey(ruleId);
    }
}

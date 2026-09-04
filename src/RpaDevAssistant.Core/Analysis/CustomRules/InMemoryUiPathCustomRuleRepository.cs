namespace RpaDevAssistant.Core.Analysis.CustomRules;

public sealed class InMemoryUiPathCustomRuleRepository : IUiPathCustomRuleRepository
{
    private readonly Dictionary<string, UiPathCustomRuleDefinition> rules = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryUiPathCustomRuleRepository(IEnumerable<UiPathCustomRuleDefinition>? rules = null)
    {
        foreach (var rule in rules ?? [])
        {
            this.rules[rule.Id] = rule;
        }
    }

    public IReadOnlyList<UiPathCustomRuleDefinition> GetRules()
    {
        return rules.Values.OrderBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public UiPathCustomRuleDefinition? GetRule(string id)
    {
        return rules.GetValueOrDefault(id);
    }

    public UiPathCustomRuleDefinition SaveRule(UiPathCustomRuleDefinition rule)
    {
        rules[rule.Id] = rule;
        return rule;
    }

    public UiPathCustomRuleImportResult ImportRules(IEnumerable<UiPathCustomRuleDefinition> rules, bool overwrite = false)
    {
        var imported = 0;
        var skipped = 0;
        foreach (var rule in rules)
        {
            if (this.rules.ContainsKey(rule.Id) && !overwrite)
            {
                skipped += 1;
                continue;
            }

            this.rules[rule.Id] = rule;
            imported += 1;
        }

        return new UiPathCustomRuleImportResult
        {
            ImportedCount = imported,
            SkippedDuplicateCount = skipped
        };
    }

    public UiPathCustomRuleStore ExportRules()
    {
        return new UiPathCustomRuleStore { Rules = GetRules() };
    }
}

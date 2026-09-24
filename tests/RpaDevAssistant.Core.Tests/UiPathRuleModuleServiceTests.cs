using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.CustomRules;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.RuleCatalog;
using RpaDevAssistant.Core.Modules;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathRuleModuleServiceTests
{
    [Fact]
    public void ExportAndImportRoundTripsDeclarativeRulesAndProfiles()
    {
        var sourceRules = new InMemoryUiPathCustomRuleRepository([Rule("CUSTOM-MODULE-1")]);
        var sourceProfiles = new InMemoryUiPathRuleProfileRepository([Profile("team-profile")]);
        var exported = new UiPathRuleModuleService(sourceRules, new UiPathCustomRuleValidator(), sourceProfiles)
            .Export("com.example.quality", "Example Quality", "1.0.0", "Example");
        var targetRules = new InMemoryUiPathCustomRuleRepository();
        var targetProfiles = new InMemoryUiPathRuleProfileRepository();

        var result = new UiPathRuleModuleService(targetRules, new UiPathCustomRuleValidator(), targetProfiles).Import(exported);

        Assert.True(result.Success);
        Assert.Equal(1, result.ImportedRules);
        Assert.Equal(1, result.ImportedProfiles);
        Assert.NotNull(targetRules.GetRule("CUSTOM-MODULE-1"));
        Assert.NotNull(targetProfiles.GetProfile("team-profile"));
    }

    [Fact]
    public void RejectsEntireModuleBeforeImportWhenAnyRuleIsInvalid()
    {
        var targetRules = new InMemoryUiPathCustomRuleRepository();
        var service = new UiPathRuleModuleService(targetRules, new UiPathCustomRuleValidator(), new InMemoryUiPathRuleProfileRepository());
        var module = Module([Rule("RPA999"), Rule("CUSTOM-VALID")]);

        var result = service.Import(module);

        Assert.False(result.Success);
        Assert.Empty(targetRules.GetRules());
        Assert.Contains(result.Errors, error => error.Contains("built-in RPA prefix", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsUnknownSchemaAndDuplicateIds()
    {
        var service = new UiPathRuleModuleService(new InMemoryUiPathCustomRuleRepository(), new UiPathCustomRuleValidator(), new InMemoryUiPathRuleProfileRepository());
        var module = Module([Rule("CUSTOM-DUP"), Rule("custom-dup")]) with { SchemaVersion = 2 };

        var result = service.Import(module);

        Assert.Contains(result.Errors, error => error.Contains("schema version", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("duplicated", StringComparison.OrdinalIgnoreCase));
    }

    private static UiPathRuleModule Module(IReadOnlyList<UiPathCustomRuleDefinition> rules) => new()
    {
        ModuleId = "com.example.module", Name = "Example", Version = "1.0.0", Rules = rules
    };

    private static UiPathCustomRuleDefinition Rule(string id) => new()
    {
        Id = id, Name = id, Category = RuleCategory.Maintainability, Severity = RuleSeverity.Warning,
        Scope = UiPathRuleScope.Activity,
        Conditions = [new UiPathRuleCondition { Field = "Activity.Name", Operator = UiPathRuleConditionOperator.Equals, Value = "Delay" }]
    };

    private static UiPathRuleProfile Profile(string id) => new()
    {
        Id = id, Name = "Team Profile", Rules = [new UiPathRuleConfiguration { RuleId = "CUSTOM-MODULE-1", Enabled = true, Weight = 2, MaxPenalty = 10 }]
    };
}

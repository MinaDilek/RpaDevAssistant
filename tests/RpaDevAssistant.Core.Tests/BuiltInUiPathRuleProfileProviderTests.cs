using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class BuiltInUiPathRuleProfileProviderTests
{
    [Fact]
    public void GetProfiles_ReturnsAllStableBuiltInProfiles()
    {
        var profiles = new BuiltInUiPathRuleProfileProvider().GetProfiles();

        Assert.Equal(
            ["default", "strict", "legacy", "reframework", "modern", "migration"],
            profiles.Select(profile => profile.Id));
        Assert.All(profiles, profile => Assert.Equal(48, profile.Rules.Count));
    }

    [Fact]
    public void StrictProfile_EnablesEveryRuleAndStrengthensSuggestionRules()
    {
        var profile = new BuiltInUiPathRuleProfileProvider().GetProfile("STRICT");

        Assert.All(profile.Rules, rule => Assert.True(rule.Enabled));
        Assert.Equal(RuleSeverity.Warning, Rule(profile, "RPA007").SeverityOverride);
        Assert.True(Rule(profile, "RPA002").Weight > Rule(new BuiltInUiPathRuleProfileProvider().GetProfile(null), "RPA002").Weight);
    }

    [Fact]
    public void LegacyProfile_DoesNotPenalizeLegacyCompatibilitySignals()
    {
        var profile = new BuiltInUiPathRuleProfileProvider().GetProfile("legacy");

        Assert.False(Rule(profile, "RPA016").Enabled);
        Assert.False(Rule(profile, "RPA028").Enabled);
        Assert.False(Rule(profile, "RPA029").Enabled);
    }

    [Fact]
    public void SpecializedProfiles_EmphasizeTheirTargetRules()
    {
        var provider = new BuiltInUiPathRuleProfileProvider();
        var defaultProfile = provider.GetProfile(null);

        Assert.True(Rule(provider.GetProfile("reframework"), "RPA030").Weight > Rule(defaultProfile, "RPA030").Weight);
        Assert.True(Rule(provider.GetProfile("modern"), "RPA016").Weight > Rule(defaultProfile, "RPA016").Weight);
        Assert.True(Rule(provider.GetProfile("migration"), "RPA027").Weight > Rule(defaultProfile, "RPA027").Weight);
        Assert.True(Rule(provider.GetProfile("migration"), "RPA048").Weight > Rule(defaultProfile, "RPA048").Weight);
    }

    private static UiPathRuleConfiguration Rule(UiPathRuleProfile profile, string ruleId)
    {
        return Assert.Single(profile.Rules, rule => rule.RuleId == ruleId);
    }
}

namespace RpaDevAssistant.Core.Analysis.Profiles;

public interface IUiPathRuleProfileProvider
{
    IReadOnlyList<UiPathRuleProfile> GetProfiles();

    UiPathRuleProfile GetProfile(string? profileId);
}

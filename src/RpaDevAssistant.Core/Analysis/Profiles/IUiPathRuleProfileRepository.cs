namespace RpaDevAssistant.Core.Analysis.Profiles;

public interface IUiPathRuleProfileRepository
{
    IReadOnlyList<UiPathRuleProfile> GetProfiles();

    UiPathRuleProfile? GetProfile(string id);

    UiPathRuleProfile SaveProfile(UiPathRuleProfile profile);

    UiPathRuleProfileStore ExportProfiles();
}

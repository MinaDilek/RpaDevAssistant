namespace RpaDevAssistant.Core.Analysis.Profiles;

public sealed class InMemoryUiPathRuleProfileRepository : IUiPathRuleProfileRepository
{
    private readonly Dictionary<string, UiPathRuleProfile> profiles;

    public InMemoryUiPathRuleProfileRepository(IEnumerable<UiPathRuleProfile>? profiles = null)
    {
        this.profiles = (profiles ?? [])
            .ToDictionary(profile => profile.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<UiPathRuleProfile> GetProfiles()
    {
        return profiles.Values.OrderBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public UiPathRuleProfile? GetProfile(string id)
    {
        return profiles.GetValueOrDefault(id);
    }

    public UiPathRuleProfile SaveProfile(UiPathRuleProfile profile)
    {
        profiles[profile.Id] = profile;
        return profile;
    }

    public UiPathRuleProfileStore ExportProfiles()
    {
        return new UiPathRuleProfileStore { Profiles = GetProfiles() };
    }
}

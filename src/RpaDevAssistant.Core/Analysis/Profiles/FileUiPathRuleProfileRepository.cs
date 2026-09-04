using System.Text.Json;
using System.Text.Json.Serialization;

namespace RpaDevAssistant.Core.Analysis.Profiles;

public sealed class FileUiPathRuleProfileRepository : IUiPathRuleProfileRepository
{
    private readonly UiPathRuleProfileOptions options;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FileUiPathRuleProfileRepository(UiPathRuleProfileOptions? options = null)
    {
        this.options = options ?? new UiPathRuleProfileOptions();
    }

    public IReadOnlyList<UiPathRuleProfile> GetProfiles()
    {
        return LoadStore().Profiles;
    }

    public UiPathRuleProfile? GetProfile(string id)
    {
        return GetProfiles().FirstOrDefault(profile => profile.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public UiPathRuleProfile SaveProfile(UiPathRuleProfile profile)
    {
        var validation = Validate(profile);
        if (validation.Count > 0)
        {
            throw new UiPathRuleProfileValidationException(validation);
        }

        var store = LoadStore();
        var profiles = store.Profiles
            .Where(existing => !existing.Id.Equals(profile.Id, StringComparison.OrdinalIgnoreCase))
            .Append(profile)
            .OrderBy(existing => existing.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        SaveStore(store with { Profiles = profiles });
        return profile;
    }

    public UiPathRuleProfileStore ExportProfiles()
    {
        return LoadStore();
    }

    private static IReadOnlyList<string> Validate(UiPathRuleProfile profile)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            errors.Add("Profile id is required.");
        }

        if (profile.Id.Equals(BuiltInUiPathRuleProfileProvider.DefaultProfileId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("The built-in default profile cannot be overwritten.");
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            errors.Add("Profile name is required.");
        }

        foreach (var rule in profile.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.RuleId))
            {
                errors.Add("RuleId is required for every profile rule.");
            }

            if (rule.Weight < 0)
            {
                errors.Add($"{rule.RuleId}: Weight cannot be negative.");
            }

            if (rule.MaxPenalty < 0)
            {
                errors.Add($"{rule.RuleId}: MaxPenalty cannot be negative.");
            }
        }

        return errors;
    }

    private UiPathRuleProfileStore LoadStore()
    {
        var path = ResolvePath();
        if (!File.Exists(path))
        {
            return new UiPathRuleProfileStore();
        }

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<UiPathRuleProfileStore>(stream, jsonOptions) ?? new UiPathRuleProfileStore();
        }
        catch (JsonException)
        {
            return new UiPathRuleProfileStore();
        }
        catch (IOException)
        {
            return new UiPathRuleProfileStore();
        }
    }

    private void SaveStore(UiPathRuleProfileStore store)
    {
        var path = ResolvePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(store, jsonOptions));
        File.Move(tempPath, path, overwrite: true);
    }

    private string ResolvePath()
    {
        if (!string.IsNullOrWhiteSpace(options.ConfigFilePath))
        {
            return Path.GetFullPath(options.ConfigFilePath);
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = AppContext.BaseDirectory;
        }

        return Path.Combine(appData, "RPA Dev Assistant", "rule-profiles.json");
    }
}

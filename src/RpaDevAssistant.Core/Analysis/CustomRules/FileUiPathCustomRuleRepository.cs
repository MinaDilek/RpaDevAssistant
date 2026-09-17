using System.Text.Json;
using System.Text.Json.Serialization;

namespace RpaDevAssistant.Core.Analysis.CustomRules;

public sealed class FileUiPathCustomRuleRepository : IUiPathCustomRuleRepository
{
    private readonly UiPathCustomRuleOptions options;
    private readonly IUiPathCustomRuleValidator validator;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FileUiPathCustomRuleRepository(
        UiPathCustomRuleOptions? options = null,
        IUiPathCustomRuleValidator? validator = null)
    {
        this.options = options ?? new UiPathCustomRuleOptions();
        this.validator = validator ?? new UiPathCustomRuleValidator();
    }

    public IReadOnlyList<UiPathCustomRuleDefinition> GetRules()
    {
        return LoadStore().Rules;
    }

    public UiPathCustomRuleDefinition? GetRule(string id)
    {
        return GetRules().FirstOrDefault(rule => rule.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public UiPathCustomRuleDefinition SaveRule(UiPathCustomRuleDefinition rule)
    {
        var store = LoadStore();
        var existingIds = store.Rules
            .Where(existing => !existing.Id.Equals(rule.Id, StringComparison.OrdinalIgnoreCase))
            .Select(existing => existing.Id);
        var validation = validator.Validate(rule, existingIds.Concat(BuiltInRuleIds()));
        if (!validation.IsValid)
        {
            throw new UiPathCustomRuleValidationException(validation.Errors);
        }

        var existingRule = store.Rules.FirstOrDefault(existing => existing.Id.Equals(rule.Id, StringComparison.OrdinalIgnoreCase));
        var now = DateTimeOffset.UtcNow;
        var ruleToSave = rule with
        {
            CreatedAtUtc = rule.CreatedAtUtc ?? existingRule?.CreatedAtUtc ?? now,
            UpdatedAtUtc = now
        };

        var rules = store.Rules
            .Where(existing => !existing.Id.Equals(rule.Id, StringComparison.OrdinalIgnoreCase))
            .Append(ruleToSave)
            .OrderBy(existing => existing.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        SaveStore(store with { Rules = rules });
        return ruleToSave;
    }

    public UiPathCustomRuleImportResult ImportRules(IEnumerable<UiPathCustomRuleDefinition> rules, bool overwrite = false)
    {
        var current = LoadStore();
        var output = current.Rules.ToDictionary(rule => rule.Id, StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var imported = 0;
        var skipped = 0;

        foreach (var rule in rules)
        {
            if (output.ContainsKey(rule.Id) && !overwrite)
            {
                skipped += 1;
                continue;
            }

            var existingIds = output.Keys.Where(id => !id.Equals(rule.Id, StringComparison.OrdinalIgnoreCase)).Concat(BuiltInRuleIds());
            var validation = validator.Validate(rule, existingIds);
            if (!validation.IsValid)
            {
                errors.AddRange(validation.Errors.Select(error => $"{rule.Id}: {error}"));
                continue;
            }

            output[rule.Id] = rule;
            imported += 1;
        }

        if (imported > 0)
        {
            SaveStore(current with { Rules = output.Values.OrderBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase).ToArray() });
        }

        return new UiPathCustomRuleImportResult
        {
            ImportedCount = imported,
            SkippedDuplicateCount = skipped,
            Errors = errors
        };
    }

    public UiPathCustomRuleStore ExportRules()
    {
        return LoadStore();
    }

    private UiPathCustomRuleStore LoadStore()
    {
        var path = ResolvePath();
        if (!File.Exists(path))
        {
            return new UiPathCustomRuleStore();
        }

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<UiPathCustomRuleStore>(stream, jsonOptions) ?? new UiPathCustomRuleStore();
        }
        catch (JsonException)
        {
            return new UiPathCustomRuleStore();
        }
        catch (IOException)
        {
            return new UiPathCustomRuleStore();
        }
    }

    private void SaveStore(UiPathCustomRuleStore store)
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

        return Path.Combine(appData, "RPA Dev Assistant", "custom-rules.json");
    }

    private static IEnumerable<string> BuiltInRuleIds()
    {
        for (var index = 1; index <= 25; index += 1)
        {
            yield return $"RPA{index:000}";
        }
    }
}

using System.Text.RegularExpressions;
using RpaDevAssistant.Core.Analysis.CustomRules;
using RpaDevAssistant.Core.Analysis.Profiles;

namespace RpaDevAssistant.Core.Modules;

public sealed partial class UiPathRuleModuleService : IUiPathRuleModuleService
{
    private readonly IUiPathCustomRuleRepository rules;
    private readonly IUiPathCustomRuleValidator validator;
    private readonly IUiPathRuleProfileRepository profiles;

    public UiPathRuleModuleService(IUiPathCustomRuleRepository rules, IUiPathCustomRuleValidator validator, IUiPathRuleProfileRepository profiles)
    {
        this.rules = rules;
        this.validator = validator;
        this.profiles = profiles;
    }

    public UiPathRuleModule Export(string moduleId, string name, string version, string? publisher = null, string? description = null)
    {
        var errors = ValidateMetadata(moduleId, name, version);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
        return new UiPathRuleModule
        {
            ModuleId = moduleId.Trim(), Name = name.Trim(), Version = version.Trim(),
            Publisher = publisher?.Trim(), Description = description?.Trim(),
            Rules = rules.ExportRules().Rules,
            Profiles = profiles.ExportProfiles().Profiles
        };
    }

    public UiPathRuleModuleImportResult Import(UiPathRuleModule module, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(module);
        var errors = ValidateMetadata(module.ModuleId, module.Name, module.Version);
        if (module.SchemaVersion != 1) errors.Add($"Unsupported module schema version '{module.SchemaVersion}'.");
        var duplicateRuleIds = module.Rules.GroupBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key);
        errors.AddRange(duplicateRuleIds.Select(id => $"Rule ID '{id}' is duplicated in the module."));
        var duplicateProfileIds = module.Profiles.GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key);
        errors.AddRange(duplicateProfileIds.Select(id => $"Profile ID '{id}' is duplicated in the module."));

        var existingRuleIds = rules.GetRules().Select(rule => rule.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in module.Rules)
        {
            var validationExisting = overwrite ? existingRuleIds.Where(id => !id.Equals(rule.Id, StringComparison.OrdinalIgnoreCase)) : existingRuleIds;
            errors.AddRange(validator.Validate(rule, validationExisting).Errors.Select(error => $"{rule.Id}: {error}"));
        }
        foreach (var profile in module.Profiles)
        {
            errors.AddRange(ValidateProfile(profile).Select(error => $"{profile.Id}: {error}"));
            if (!overwrite && profiles.GetProfile(profile.Id) is not null) errors.Add($"Profile ID '{profile.Id}' already exists.");
        }
        if (errors.Count > 0) return new UiPathRuleModuleImportResult { Errors = errors.Distinct().ToArray() };

        var ruleResult = rules.ImportRules(module.Rules, overwrite);
        if (ruleResult.Errors.Count > 0) return new UiPathRuleModuleImportResult { Errors = ruleResult.Errors };
        var importedProfiles = 0;
        var skippedProfiles = 0;
        foreach (var profile in module.Profiles)
        {
            if (!overwrite && profiles.GetProfile(profile.Id) is not null) { skippedProfiles++; continue; }
            profiles.SaveProfile(profile);
            importedProfiles++;
        }
        return new UiPathRuleModuleImportResult
        {
            ImportedRules = ruleResult.ImportedCount,
            SkippedRules = ruleResult.SkippedDuplicateCount,
            ImportedProfiles = importedProfiles,
            SkippedProfiles = skippedProfiles
        };
    }

    private static List<string> ValidateMetadata(string? moduleId, string? name, string? version)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(moduleId) || !ModuleIdPattern().IsMatch(moduleId)) errors.Add("ModuleId must use letters, numbers, dots, dashes, or underscores.");
        if (string.IsNullOrWhiteSpace(name)) errors.Add("Module name is required.");
        if (string.IsNullOrWhiteSpace(version) || !Version.TryParse(version, out _)) errors.Add("Module version must be a numeric version such as 1.0.0.");
        return errors;
    }

    private static IReadOnlyList<string> ValidateProfile(UiPathRuleProfile profile)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.Id)) errors.Add("Profile id is required.");
        if (profile.Id.Equals(BuiltInUiPathRuleProfileProvider.DefaultProfileId, StringComparison.OrdinalIgnoreCase)) errors.Add("The built-in default profile cannot be overwritten.");
        if (string.IsNullOrWhiteSpace(profile.Name)) errors.Add("Profile name is required.");
        if (profile.Rules.Any(rule => string.IsNullOrWhiteSpace(rule.RuleId) || rule.Weight < 0 || rule.MaxPenalty < 0)) errors.Add("Every profile rule needs an ID and non-negative scoring values.");
        return errors;
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{1,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex ModuleIdPattern();
}

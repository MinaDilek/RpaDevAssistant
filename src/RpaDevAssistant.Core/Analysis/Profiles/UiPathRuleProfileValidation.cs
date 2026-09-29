namespace RpaDevAssistant.Core.Analysis.Profiles;

public static class UiPathRuleProfileValidation
{
    public static IReadOnlyList<string> Validate(UiPathRuleProfile profile, bool protectBuiltInDefault = true)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.Id)) errors.Add("Profile id is required.");
        if (protectBuiltInDefault && profile.Id.Equals(BuiltInUiPathRuleProfileProvider.DefaultProfileId, StringComparison.OrdinalIgnoreCase))
            errors.Add("The built-in default profile cannot be overwritten.");
        if (string.IsNullOrWhiteSpace(profile.Name)) errors.Add("Profile name is required.");

        var duplicateRuleIds = profile.Rules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.RuleId))
            .GroupBy(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        foreach (var duplicate in duplicateRuleIds) errors.Add($"{duplicate}: RuleId must be unique within a profile.");

        foreach (var rule in profile.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.RuleId)) errors.Add("RuleId is required for every profile rule.");
            if (rule.Weight < 0) errors.Add($"{rule.RuleId}: Weight cannot be negative.");
            if (rule.MaxPenalty < 0) errors.Add($"{rule.RuleId}: MaxPenalty cannot be negative.");
        }

        return errors;
    }
}

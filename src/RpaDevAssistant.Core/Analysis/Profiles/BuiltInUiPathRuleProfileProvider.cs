namespace RpaDevAssistant.Core.Analysis.Profiles;

using RpaDevAssistant.Core.Analysis.CustomRules;

public sealed class BuiltInUiPathRuleProfileProvider : IUiPathRuleProfileProvider
{
    public const string DefaultProfileId = "default";

    private static readonly UiPathRuleProfile DefaultProfile = new()
    {
        Id = DefaultProfileId,
        Name = "Default",
        Description = "Balanced built-in profile for general UiPath project review.",
        Rules =
        [
            new UiPathRuleConfiguration
            {
                RuleId = "RPA001",
                Enabled = true,
                Weight = 2,
                MaxPenalty = 10,
                Description = "Avoid Delay Activities"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA002",
                Enabled = true,
                Weight = 10,
                MaxPenalty = 30,
                Description = "Empty Catch Block"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA003",
                Enabled = true,
                Weight = 12,
                MaxPenalty = 30,
                Description = "Exception Silently Swallowed"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA004",
                Enabled = true,
                Weight = 4,
                MaxPenalty = 12,
                Description = "Long Hard-Coded Delay"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA005",
                Enabled = true,
                Weight = 12,
                MaxPenalty = 30,
                Description = "Invalid Invoke Workflow Reference"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA006",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 5,
                Description = "Workflow Naming Convention"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA007",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 10,
                Description = "Generic Activity Display Name"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA008",
                Enabled = true,
                Weight = 2,
                MaxPenalty = 10,
                Description = "Workflow Has No Logging"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA009",
                Enabled = true,
                Weight = 15,
                MaxPenalty = 30,
                Description = "Hard-Coded Credential-Like Value"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA010",
                Enabled = true,
                Weight = 10,
                MaxPenalty = 20,
                Description = "Potential Sensitive Data Logging"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA011",
                Enabled = true,
                Weight = 3,
                MaxPenalty = 10,
                Description = "Excessive UI Timeout"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA012",
                Enabled = true,
                Weight = 3,
                MaxPenalty = 10,
                Description = "Unrealistically Low UI Timeout"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA013",
                Enabled = true,
                Weight = 4,
                MaxPenalty = 12,
                Description = "ContinueOnError Enabled"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA014",
                Enabled = true,
                Weight = 6,
                MaxPenalty = 18,
                Description = "Excessive ContinueOnError Usage"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA015",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 8,
                Description = "Missing Explicit Timeout on Critical UI Activity"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA016",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 8,
                Description = "Legacy UI Automation Activity"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA017",
                Enabled = true,
                Weight = 3,
                MaxPenalty = 12,
                Description = "Selector Uses idx Attribute"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA018",
                Enabled = true,
                Weight = 3,
                MaxPenalty = 12,
                Description = "Potentially Unstable Selector Attribute"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA019",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 8,
                Description = "Overly Complex Selector"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA020",
                Enabled = true,
                Weight = 3,
                MaxPenalty = 10,
                Description = "Hard-Coded Absolute File Path"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA021",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 8,
                Description = "Hard-Coded Email Address"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA022",
                Enabled = true,
                Weight = 5,
                MaxPenalty = 15,
                Description = "HTTP Request Without Explicit Timeout"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA023",
                Enabled = true,
                Weight = 5,
                MaxPenalty = 15,
                Description = "HTTP Request Without Local Error Handling"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA024",
                Enabled = true,
                Weight = 2,
                MaxPenalty = 8,
                Description = "Excessive Workflow Arguments"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA025",
                Enabled = true,
                Weight = 5,
                MaxPenalty = 15,
                Description = "Large Workflow"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA026",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 8,
                Description = "Possibly Unused Dependency"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA027",
                Enabled = true,
                Weight = 4,
                MaxPenalty = 12,
                Description = "Package Version Alignment Risk"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA028",
                Enabled = true,
                Weight = 0,
                MaxPenalty = 0,
                Description = "Mixed Modern and Classic Activity Usage"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA029",
                Enabled = true,
                Weight = 2,
                MaxPenalty = 8,
                Description = "Legacy Package Indicator"
            }
        ]
    };

    private readonly IUiPathCustomRuleRepository? customRuleRepository;
    private readonly IUiPathRuleProfileRepository? customProfileRepository;

    public BuiltInUiPathRuleProfileProvider(
        IUiPathCustomRuleRepository? customRuleRepository = null,
        IUiPathRuleProfileRepository? customProfileRepository = null)
    {
        this.customRuleRepository = customRuleRepository;
        this.customProfileRepository = customProfileRepository;
    }

    public IReadOnlyList<UiPathRuleProfile> GetProfiles()
    {
        var defaultProfile = BuildDefaultProfile();
        if (customProfileRepository is null)
        {
            return [defaultProfile];
        }

        return customProfileRepository.GetProfiles()
            .Where(profile => !profile.Id.Equals(DefaultProfileId, StringComparison.OrdinalIgnoreCase))
            .Select(profile => MergeWithDefault(defaultProfile, profile))
            .Prepend(defaultProfile)
            .ToArray();
    }

    public UiPathRuleProfile GetProfile(string? profileId)
    {
        var effectiveProfileId = string.IsNullOrWhiteSpace(profileId) ? DefaultProfileId : profileId;
        return GetProfiles().FirstOrDefault(profile => profile.Id.Equals(effectiveProfileId, StringComparison.OrdinalIgnoreCase))
            ?? throw new UnknownRuleProfileException(effectiveProfileId);
    }

    private UiPathRuleProfile BuildDefaultProfile()
    {
        if (customRuleRepository is null)
        {
            return DefaultProfile;
        }

        var customConfigurations = customRuleRepository.GetRules()
            .Select(rule => new UiPathRuleConfiguration
            {
                RuleId = rule.Id,
                Enabled = rule.Enabled,
                Weight = rule.Weight,
                MaxPenalty = rule.MaxPenalty,
                Description = rule.Name
            })
            .ToArray();

        if (customConfigurations.Length == 0)
        {
            return DefaultProfile;
        }

        return DefaultProfile with
        {
            Rules = DefaultProfile.Rules.Concat(customConfigurations).ToArray()
        };
    }

    private static UiPathRuleProfile MergeWithDefault(UiPathRuleProfile defaultProfile, UiPathRuleProfile customProfile)
    {
        var overrides = customProfile.Rules.ToDictionary(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase);
        var merged = defaultProfile.Rules
            .Select(rule => overrides.TryGetValue(rule.RuleId, out var overrideRule) ? overrideRule : rule)
            .ToList();

        merged.AddRange(customProfile.Rules.Where(rule => !defaultProfile.Rules.Any(defaultRule => defaultRule.RuleId.Equals(rule.RuleId, StringComparison.OrdinalIgnoreCase))));

        return customProfile with { Rules = merged };
    }
}

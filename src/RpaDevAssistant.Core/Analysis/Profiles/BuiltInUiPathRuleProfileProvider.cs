namespace RpaDevAssistant.Core.Analysis.Profiles;

using RpaDevAssistant.Core.Analysis.CustomRules;

public sealed class BuiltInUiPathRuleProfileProvider : IUiPathRuleProfileProvider
{
    public const string DefaultProfileId = "default";
    public const string StrictProfileId = "strict";
    public const string LegacyProfileId = "legacy";
    public const string ReFrameworkProfileId = "reframework";
    public const string ModernProfileId = "modern";
    public const string MigrationProfileId = "migration";

    private static readonly string[] BuiltInProfileIds =
    [
        DefaultProfileId,
        StrictProfileId,
        LegacyProfileId,
        ReFrameworkProfileId,
        ModernProfileId,
        MigrationProfileId
    ];

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
                Enabled = false,
                Weight = 1,
                MaxPenalty = 8,
                Description = "Missing Explicit Timeout on Critical UI Activity - disabled by default because timeout defaults vary by UiPath project type."
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
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA030",
                Enabled = true,
                Weight = 5,
                MaxPenalty = 15,
                Description = "BusinessRuleException Handling"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA031",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 5,
                Description = "Argument Naming Convention"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA032",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 5,
                Description = "Variable Naming Convention"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA033",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 8,
                Description = "Unused Variable"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA034",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 8,
                Description = "Unused Argument"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA035",
                Enabled = true,
                Weight = 3,
                MaxPenalty = 10,
                Description = "Hard-Coded URL"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA036",
                Enabled = true,
                Weight = 10,
                MaxPenalty = 20,
                Description = "Circular Workflow Reference"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA037",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 8,
                Description = "Unused Workflow"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA038",
                Enabled = true,
                Weight = 2,
                MaxPenalty = 10,
                Description = "Argument Direction Mismatch"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA039",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 6,
                Description = "Unnecessary InOut Argument"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA040",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 8,
                Description = "Invalid Argument Type Declaration"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA041",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 6,
                Description = "Overly Broad Variable Scope"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA042",
                Enabled = true,
                Weight = 2,
                MaxPenalty = 10,
                Description = "Shadowed Variable"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA043",
                Enabled = true,
                Weight = 2,
                MaxPenalty = 10,
                Description = "Invalid Invoke Workflow Argument Mapping"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA044",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 8,
                Description = "Missing Invoke Workflow Argument Mapping"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA045",
                Enabled = true,
                Weight = 2,
                MaxPenalty = 10,
                Description = "Invoke Workflow Argument Direction Mismatch"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA046",
                Enabled = true,
                Weight = 1,
                MaxPenalty = 5,
                Description = "Fixed Delays Without State-Based Wait"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA047",
                Enabled = true,
                Weight = 3,
                MaxPenalty = 12,
                Description = "Hard-Coded Queue Name"
            },
            new UiPathRuleConfiguration
            {
                RuleId = "RPA048",
                Enabled = true,
                Weight = 2,
                MaxPenalty = 10,
                Description = "Invalid Argument Default Value"
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
        var builtInProfiles = BuildBuiltInProfiles(defaultProfile);
        if (customProfileRepository is null)
        {
            return builtInProfiles;
        }

        var customProfiles = customProfileRepository.GetProfiles()
            .Where(profile => !BuiltInProfileIds.Contains(profile.Id, StringComparer.OrdinalIgnoreCase))
            .Select(profile => MergeWithDefault(defaultProfile, profile))
            .ToArray();

        return builtInProfiles.Concat(customProfiles).ToArray();
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
            .Where(rule => !rule.IsTemplate)
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

    private static IReadOnlyList<UiPathRuleProfile> BuildBuiltInProfiles(UiPathRuleProfile defaultProfile)
    {
        return
        [
            defaultProfile,
            CreateProfile(
                defaultProfile,
                StrictProfileId,
                "Strict",
                "High-assurance profile with stronger penalties and all deterministic checks enabled.",
                rule => rule with
                {
                    Enabled = true,
                    Weight = Scale(rule.Weight, 1.5),
                    MaxPenalty = Scale(rule.MaxPenalty, 1.5),
                    SeverityOverride = rule.RuleId is "RPA006" or "RPA007" or "RPA015" or "RPA019" or "RPA021" or "RPA024"
                        ? RuleSeverity.Warning
                        : rule.SeverityOverride
                }),
            CreateProfile(
                defaultProfile,
                LegacyProfileId,
                "Legacy UiPath",
                "Compatibility-focused profile for Windows-Legacy and Classic projects.",
                rule => rule.RuleId switch
                {
                    "RPA015" or "RPA016" or "RPA028" or "RPA029" => rule with
                    {
                        Enabled = false,
                        Weight = 0,
                        MaxPenalty = 0
                    },
                    _ => rule
                }),
            CreateProfile(
                defaultProfile,
                ReFrameworkProfileId,
                "REFramework",
                "Profile emphasizing transaction, exception, invocation, Queue and argument contracts in REFramework projects.",
                rule => rule.RuleId is "RPA002" or "RPA003" or "RPA005" or "RPA030" or "RPA036" or "RPA043" or "RPA044" or "RPA045" or "RPA047"
                    ? rule with
                    {
                        Weight = Scale(rule.Weight, 1.5),
                        MaxPenalty = Scale(rule.MaxPenalty, 1.5)
                    }
                    : rule),
            CreateProfile(
                defaultProfile,
                ModernProfileId,
                "Modern UiPath",
                "Profile for Windows/Modern projects with stronger UI Automation and legacy-usage checks.",
                rule => rule.RuleId switch
                {
                    "RPA015" => rule with { Enabled = true },
                    "RPA016" or "RPA017" or "RPA018" or "RPA019" or "RPA028" or "RPA029" => rule with
                    {
                        Weight = Scale(rule.Weight, 1.5),
                        MaxPenalty = Scale(rule.MaxPenalty, 1.5)
                    },
                    _ => rule
                }),
            CreateProfile(
                defaultProfile,
                MigrationProfileId,
                "Migration Ready",
                "Profile emphasizing package, compatibility, legacy activity and workflow contract risks before migration.",
                rule => rule.RuleId switch
                {
                    "RPA015" => rule with { Enabled = true },
                    "RPA016" or "RPA026" or "RPA027" or "RPA028" or "RPA029" or "RPA035" or "RPA036" or "RPA038" or "RPA040" or "RPA043" or "RPA044" or "RPA045" or "RPA048" => rule with
                    {
                        Weight = Scale(rule.Weight, 1.75),
                        MaxPenalty = Scale(rule.MaxPenalty, 1.75)
                    },
                    _ => rule
                })
        ];
    }

    private static UiPathRuleProfile CreateProfile(
        UiPathRuleProfile source,
        string id,
        string name,
        string description,
        Func<UiPathRuleConfiguration, UiPathRuleConfiguration> configure)
    {
        return new UiPathRuleProfile
        {
            Id = id,
            Name = name,
            Description = description,
            Rules = source.Rules.Select(configure).ToArray()
        };
    }

    private static double Scale(double value, double multiplier)
    {
        return Math.Round(value * multiplier, 2, MidpointRounding.AwayFromZero);
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

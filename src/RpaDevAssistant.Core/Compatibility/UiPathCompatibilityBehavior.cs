using RpaDevAssistant.Core.Dependencies;

namespace RpaDevAssistant.Core.Compatibility;

public enum UiPathRuntimeCompatibility
{
    Unknown,
    Windows,
    WindowsLegacy,
    CrossPlatform
}

public enum UiPathDesignExperience
{
    Unknown,
    Modern,
    Classic,
    Mixed
}

public sealed record UiPathCompatibilityBehavior
{
    public UiPathRuntimeCompatibility Runtime { get; init; }

    public UiPathDesignExperience DesignExperience { get; init; }

    public bool SupportsModernActivities { get; init; }

    public bool SupportsClassicActivities { get; init; }

    public bool PreferModernUiAutomation { get; init; }

    public bool FlagLegacyUiActivities { get; init; }

    public bool MigrationRecommended { get; init; }
}

public interface IUiPathCompatibilityResolver
{
    UiPathCompatibilityBehavior Resolve(string? compatibility, UiPathModernClassicMode activityMode);
}

public sealed class UiPathCompatibilityResolver : IUiPathCompatibilityResolver
{
    public UiPathCompatibilityBehavior Resolve(string? compatibility, UiPathModernClassicMode activityMode)
    {
        var normalized = Normalize(compatibility);
        var runtime = ResolveRuntime(normalized);
        var experience = ResolveExperience(normalized, activityMode, runtime);

        return runtime switch
        {
            UiPathRuntimeCompatibility.WindowsLegacy => new UiPathCompatibilityBehavior
            {
                Runtime = runtime,
                DesignExperience = experience,
                SupportsClassicActivities = true,
                SupportsModernActivities = false,
                PreferModernUiAutomation = false,
                FlagLegacyUiActivities = false,
                MigrationRecommended = true
            },
            UiPathRuntimeCompatibility.Windows => new UiPathCompatibilityBehavior
            {
                Runtime = runtime,
                DesignExperience = experience,
                SupportsClassicActivities = true,
                SupportsModernActivities = true,
                PreferModernUiAutomation = true,
                FlagLegacyUiActivities = true,
                MigrationRecommended = experience is UiPathDesignExperience.Classic or UiPathDesignExperience.Mixed
            },
            UiPathRuntimeCompatibility.CrossPlatform => new UiPathCompatibilityBehavior
            {
                Runtime = runtime,
                DesignExperience = experience,
                SupportsClassicActivities = false,
                SupportsModernActivities = true,
                PreferModernUiAutomation = true,
                FlagLegacyUiActivities = true,
                MigrationRecommended = experience is UiPathDesignExperience.Classic or UiPathDesignExperience.Mixed
            },
            _ => new UiPathCompatibilityBehavior
            {
                Runtime = runtime,
                DesignExperience = experience,
                SupportsClassicActivities = experience is UiPathDesignExperience.Classic or UiPathDesignExperience.Mixed,
                SupportsModernActivities = experience is UiPathDesignExperience.Modern or UiPathDesignExperience.Mixed,
                PreferModernUiAutomation = experience == UiPathDesignExperience.Modern,
                FlagLegacyUiActivities = experience == UiPathDesignExperience.Modern,
                MigrationRecommended = false
            }
        };
    }

    private static UiPathRuntimeCompatibility ResolveRuntime(string compatibility)
    {
        if (compatibility.Contains("windowslegacy", StringComparison.Ordinal))
        {
            return UiPathRuntimeCompatibility.WindowsLegacy;
        }

        if (compatibility.Contains("crossplatform", StringComparison.Ordinal) ||
            compatibility.Contains("portable", StringComparison.Ordinal))
        {
            return UiPathRuntimeCompatibility.CrossPlatform;
        }

        if (compatibility.Contains("windows", StringComparison.Ordinal))
        {
            return UiPathRuntimeCompatibility.Windows;
        }

        return UiPathRuntimeCompatibility.Unknown;
    }

    private static UiPathDesignExperience ResolveExperience(
        string compatibility,
        UiPathModernClassicMode activityMode,
        UiPathRuntimeCompatibility runtime)
    {
        if (activityMode != UiPathModernClassicMode.Unknown)
        {
            return activityMode switch
            {
                UiPathModernClassicMode.Modern => UiPathDesignExperience.Modern,
                UiPathModernClassicMode.Classic => UiPathDesignExperience.Classic,
                UiPathModernClassicMode.Mixed => UiPathDesignExperience.Mixed,
                _ => UiPathDesignExperience.Unknown
            };
        }

        if (compatibility.Contains("modern", StringComparison.Ordinal))
        {
            return UiPathDesignExperience.Modern;
        }

        if (compatibility.Contains("classic", StringComparison.Ordinal) || runtime == UiPathRuntimeCompatibility.WindowsLegacy)
        {
            return UiPathDesignExperience.Classic;
        }

        return UiPathDesignExperience.Unknown;
    }

    private static string Normalize(string? value)
    {
        return string.Concat((value ?? string.Empty)
            .Where(character => char.IsLetterOrDigit(character)))
            .ToLowerInvariant();
    }
}

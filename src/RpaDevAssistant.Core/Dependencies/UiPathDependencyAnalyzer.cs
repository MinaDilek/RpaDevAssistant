using System.Globalization;
using System.Text.RegularExpressions;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Dependencies;

public sealed class UiPathDependencyAnalyzer : IUiPathDependencyAnalyzer
{
    private static readonly Regex VersionRegex = new(@"(?<major>\d+)(?:\.(?<minor>\d+))?", RegexOptions.Compiled);
    private readonly IUiPathPackageActivityMapper mapper;

    public UiPathDependencyAnalyzer(IUiPathPackageActivityMapper? mapper = null)
    {
        this.mapper = mapper ?? new UiPathPackageActivityMapper();
    }

    public UiPathDependencySummary Analyze(ProjectScanResult project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var usage = BuildUsageIndex(project);
        var majorVersions = project.Dependencies
            .Select(dependency => new { dependency.Name, Major = TryReadMajorVersion(dependency.Version) })
            .Where(item => item.Major is >= 10 && item.Name.StartsWith("UiPath.", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var hasMajorAlignmentRisk = majorVersions.Select(item => item.Major!.Value).Distinct().Count() > 1
            && majorVersions.Max(item => item.Major!.Value) - majorVersions.Min(item => item.Major!.Value) >= 3;
        var mode = DetectModernClassicMode(project);

        var packages = project.Dependencies
            .OrderBy(dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
            .Select(dependency => AnalyzeDependency(dependency, usage, hasMajorAlignmentRisk, mode))
            .ToArray();

        return new UiPathDependencySummary
        {
            TotalDependencies = packages.Length,
            UiPathDependencies = packages.Count(package => package.IsUiPathPackage),
            ThirdPartyDependencies = packages.Count(package => !package.IsUiPathPackage),
            UsedDependencies = packages.Count(package => package.UsageStatus == UiPathDependencyUsageStatus.Used),
            PossiblyUnusedDependencies = packages.Count(package => package.UsageStatus == UiPathDependencyUsageStatus.PossiblyUnused),
            PotentialConflicts = packages.Count(package => package.CompatibilityStatus == UiPathDependencyCompatibilityStatus.PotentialConflict),
            LegacyIndicators = packages.Count(package => package.VersionStatus == UiPathDependencyVersionStatus.Legacy),
            ModernClassicMode = mode,
            Packages = packages
        };
    }

    private UiPathDependencyAnalysis AnalyzeDependency(
        UiPathDependency dependency,
        IReadOnlyDictionary<string, PackageUsage> usage,
        bool hasMajorAlignmentRisk,
        UiPathModernClassicMode mode)
    {
        var mapping = mapper.ClassifyPackage(dependency.Name);
        usage.TryGetValue(mapping.Family, out var packageUsage);
        packageUsage ??= new PackageUsage();
        var findings = new List<string>();
        var usageStatus = ResolveUsageStatus(mapping, packageUsage);
        var compatibilityStatus = UiPathDependencyCompatibilityStatus.Compatible;
        var versionStatus = UiPathDependencyVersionStatus.Unknown;
        var risk = UiPathDependencyRiskLevel.Low;

        if (usageStatus == UiPathDependencyUsageStatus.PossiblyUnused)
        {
            findings.Add("No parsed activity was mapped to this known package family. It may still be used indirectly or by custom code.");
            risk = Max(risk, UiPathDependencyRiskLevel.Medium);
        }

        if (hasMajorAlignmentRisk && mapping.IsUiPathPackage && TryReadMajorVersion(dependency.Version) is >= 10)
        {
            compatibilityStatus = UiPathDependencyCompatibilityStatus.PotentialConflict;
            findings.Add("UiPath package major versions differ significantly from other declared UiPath packages. This is an offline heuristic.");
            risk = Max(risk, UiPathDependencyRiskLevel.Medium);
        }

        if (mapping.IsLegacyIndicator)
        {
            versionStatus = UiPathDependencyVersionStatus.Legacy;
            findings.Add("Package name contains a legacy/classic indicator in the offline package catalog.");
            risk = Max(risk, UiPathDependencyRiskLevel.Medium);
        }

        if (mode == UiPathModernClassicMode.Mixed && mapping.Category == UiPathPackageCategory.UIAutomation)
        {
            findings.Add("Project contains both modern and classic UI automation activity signals.");
        }

        return new UiPathDependencyAnalysis
        {
            Name = dependency.Name,
            DeclaredVersion = dependency.Version,
            ResolvedVersion = NormalizeVersion(dependency.Version),
            PackageFamily = mapping.Family,
            Category = mapping.Category,
            IsUiPathPackage = mapping.IsUiPathPackage,
            IsDirectDependency = true,
            UsageStatus = usageStatus,
            CompatibilityStatus = compatibilityStatus,
            VersionStatus = versionStatus,
            RiskLevel = risk,
            Findings = findings,
            UsedActivities = packageUsage.Activities.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            UsedByWorkflows = packageUsage.Workflows.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            Notes = mapping.Notes
        };
    }

    private IReadOnlyDictionary<string, PackageUsage> BuildUsageIndex(ProjectScanResult project)
    {
        var result = new Dictionary<string, PackageUsage>(StringComparer.OrdinalIgnoreCase);
        foreach (var workflow in project.Workflows)
        {
            foreach (var activity in workflow.Analysis?.Activities ?? [])
            {
                var family = mapper.MapActivityToPackageFamily(activity);
                if (string.IsNullOrWhiteSpace(family))
                {
                    continue;
                }

                if (!result.TryGetValue(family, out var usage))
                {
                    usage = new PackageUsage();
                    result[family] = usage;
                }

                usage.Workflows.Add(workflow.RelativePath);
                usage.Activities.Add(activity.Name);
            }
        }

        return result;
    }

    private UiPathModernClassicMode DetectModernClassicMode(ProjectScanResult project)
    {
        var hasModern = false;
        var hasClassic = false;
        foreach (var activity in project.Workflows.SelectMany(workflow => workflow.Analysis?.Activities ?? []))
        {
            var signal = mapper.ClassifyActivityMode(activity);
            hasModern |= signal == UiPathModernClassicSignal.Modern;
            hasClassic |= signal == UiPathModernClassicSignal.Classic;
        }

        return (hasModern, hasClassic) switch
        {
            (true, true) => UiPathModernClassicMode.Mixed,
            (true, false) => UiPathModernClassicMode.Modern,
            (false, true) => UiPathModernClassicMode.Classic,
            _ => UiPathModernClassicMode.Unknown
        };
    }

    private static UiPathDependencyUsageStatus ResolveUsageStatus(UiPathPackageMapping mapping, PackageUsage usage)
    {
        if (usage.Workflows.Count > 0)
        {
            return UiPathDependencyUsageStatus.Used;
        }

        if (mapping.HasKnownActivityMapping)
        {
            return UiPathDependencyUsageStatus.PossiblyUnused;
        }

        return UiPathDependencyUsageStatus.Unknown;
    }

    private static int? TryReadMajorVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var match = VersionRegex.Match(version);
        return match.Success && int.TryParse(match.Groups["major"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var major)
            ? major
            : null;
    }

    private static string? NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var match = VersionRegex.Match(version);
        return match.Success ? match.Value : null;
    }

    private static UiPathDependencyRiskLevel Max(UiPathDependencyRiskLevel left, UiPathDependencyRiskLevel right)
    {
        return left >= right ? left : right;
    }

    private sealed class PackageUsage
    {
        public HashSet<string> Workflows { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Activities { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

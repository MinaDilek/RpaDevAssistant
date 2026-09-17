using System.Globalization;
using System.Text.RegularExpressions;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Dependencies;

public sealed class UiPathDependencyAnalyzer : IUiPathDependencyAnalyzer
{
    private static readonly Regex VersionRegex = new(@"(?<major>\d+)(?:\.(?<minor>\d+))?", RegexOptions.Compiled);
    private readonly IUiPathPackageActivityMapper mapper;
    private readonly IUiPathPackageMetadataProvider? metadataProvider;

    public UiPathDependencyAnalyzer(
        IUiPathPackageActivityMapper? mapper = null,
        IUiPathPackageMetadataProvider? metadataProvider = null)
    {
        this.mapper = mapper ?? new UiPathPackageActivityMapper();
        this.metadataProvider = metadataProvider;
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
            UnknownMetadataDependencies = packages.Length,
            ModernClassicMode = mode,
            Packages = packages
        };
    }

    public async Task<UiPathDependencySummary> AnalyzeAsync(
        ProjectScanResult project,
        CancellationToken cancellationToken = default)
    {
        var offlineSummary = Analyze(project);
        if (metadataProvider is null || offlineSummary.Packages.Count == 0)
        {
            return offlineSummary;
        }

        var enrichedPackages = await Task.WhenAll(offlineSummary.Packages.Select(package =>
            EnrichPackageAsync(package, cancellationToken))).ConfigureAwait(false);

        return offlineSummary with
        {
            LegacyIndicators = enrichedPackages.Count(package => package.VersionStatus == UiPathDependencyVersionStatus.Legacy),
            OutdatedDependencies = enrichedPackages.Count(package => package.VersionStatus == UiPathDependencyVersionStatus.Outdated),
            DeprecatedDependencies = enrichedPackages.Count(package => package.DeprecationStatus == UiPathPackageDeprecationStatus.Deprecated),
            VulnerableDependencies = enrichedPackages.Count(package => package.VulnerabilityStatus == UiPathPackageVulnerabilityStatus.Known),
            UnknownMetadataDependencies = enrichedPackages.Count(package => package.MetadataStatus == UiPathPackageMetadataStatus.Unknown),
            Packages = enrichedPackages
        };
    }

    private async Task<UiPathDependencyAnalysis> EnrichPackageAsync(
        UiPathDependencyAnalysis package,
        CancellationToken cancellationToken)
    {
        UiPathPackageMetadata metadata;
        try
        {
            metadata = await metadataProvider!.GetMetadataAsync(
                package.Name,
                package.ResolvedVersion,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            metadata = UiPathPackageMetadata.Unknown;
        }

        if (metadata.Status == UiPathPackageMetadataStatus.Unknown)
        {
            return package with { MetadataStatus = UiPathPackageMetadataStatus.Unknown };
        }

        var findings = package.Findings.ToList();
        var versionStatus = package.VersionStatus;
        var risk = package.RiskLevel;
        if (versionStatus != UiPathDependencyVersionStatus.Legacy
            && IsVersionOlder(package.ResolvedVersion, metadata.LatestVersion))
        {
            versionStatus = UiPathDependencyVersionStatus.Outdated;
            findings.Add($"A newer stable package version is available ({metadata.LatestVersion}).");
            risk = Max(risk, UiPathDependencyRiskLevel.Medium);
        }
        else if (versionStatus != UiPathDependencyVersionStatus.Legacy
                 && VersionsEqual(package.ResolvedVersion, metadata.LatestVersion))
        {
            versionStatus = UiPathDependencyVersionStatus.Current;
        }

        if (metadata.DeprecationStatus == UiPathPackageDeprecationStatus.Deprecated)
        {
            findings.Add(metadata.AlternatePackage is null
                ? "The declared package version is deprecated on NuGet."
                : $"The declared package version is deprecated on NuGet; suggested alternate package: {metadata.AlternatePackage}.");
            risk = Max(risk, UiPathDependencyRiskLevel.High);
        }

        if (metadata.VulnerabilityStatus == UiPathPackageVulnerabilityStatus.Known)
        {
            findings.Add($"The declared package version has {metadata.Vulnerabilities.Count} known NuGet vulnerability advisory item(s).");
            risk = Max(risk, RiskFromVulnerabilities(metadata.Vulnerabilities));
        }

        return package with
        {
            MetadataStatus = metadata.Status,
            LatestVersion = metadata.LatestVersion,
            DeprecationStatus = metadata.DeprecationStatus,
            DeprecationReasons = metadata.DeprecationReasons,
            AlternatePackage = metadata.AlternatePackage,
            VulnerabilityStatus = metadata.VulnerabilityStatus,
            Vulnerabilities = metadata.Vulnerabilities,
            MetadataCheckedAtUtc = metadata.RetrievedAtUtc,
            VersionStatus = versionStatus,
            RiskLevel = risk,
            Findings = findings
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

    private static bool IsVersionOlder(string? current, string? latest) =>
        TryParseComparableVersion(current, out var currentVersion)
        && TryParseComparableVersion(latest, out var latestVersion)
        && currentVersion.CompareTo(latestVersion) < 0;

    private static bool VersionsEqual(string? current, string? latest) =>
        TryParseComparableVersion(current, out var currentVersion)
        && TryParseComparableVersion(latest, out var latestVersion)
        && currentVersion.CompareTo(latestVersion) == 0;

    private static bool TryParseComparableVersion(string? value, out Version version)
    {
        version = new Version();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var core = value.Split('-', 2)[0].Split('+', 2)[0];
        var components = core.Split('.');
        if (components.Length is < 1 or > 4 || components.Any(component => !int.TryParse(component, out _)))
        {
            return false;
        }

        var normalized = string.Join('.', components.Concat(Enumerable.Repeat("0", 4 - components.Length)));
        return Version.TryParse(normalized, out version!);
    }

    private static UiPathDependencyRiskLevel RiskFromVulnerabilities(IReadOnlyList<UiPathPackageVulnerability> vulnerabilities)
    {
        var maximumSeverity = vulnerabilities.Count == 0 ? 0 : vulnerabilities.Max(item => item.Severity);
        return maximumSeverity switch
        {
            >= 3 => UiPathDependencyRiskLevel.Critical,
            2 => UiPathDependencyRiskLevel.High,
            _ => UiPathDependencyRiskLevel.Medium
        };
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

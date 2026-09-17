namespace RpaDevAssistant.Core.Dependencies;

public sealed record UiPathDependencyAnalysis
{
    public required string Name { get; init; }

    public string? DeclaredVersion { get; init; }

    public string? ResolvedVersion { get; init; }

    public required string PackageFamily { get; init; }

    public UiPathPackageCategory Category { get; init; } = UiPathPackageCategory.Other;

    public bool IsUiPathPackage { get; init; }

    public bool IsDirectDependency { get; init; } = true;

    public UiPathDependencyUsageStatus UsageStatus { get; init; } = UiPathDependencyUsageStatus.Unknown;

    public UiPathDependencyCompatibilityStatus CompatibilityStatus { get; init; } = UiPathDependencyCompatibilityStatus.Unknown;

    public UiPathDependencyVersionStatus VersionStatus { get; init; } = UiPathDependencyVersionStatus.Unknown;

    public UiPathDependencyRiskLevel RiskLevel { get; init; } = UiPathDependencyRiskLevel.Low;

    public UiPathPackageMetadataStatus MetadataStatus { get; init; } = UiPathPackageMetadataStatus.Unknown;

    public string? LatestVersion { get; init; }

    public UiPathPackageDeprecationStatus DeprecationStatus { get; init; } = UiPathPackageDeprecationStatus.Unknown;

    public IReadOnlyList<string> DeprecationReasons { get; init; } = [];

    public string? AlternatePackage { get; init; }

    public UiPathPackageVulnerabilityStatus VulnerabilityStatus { get; init; } = UiPathPackageVulnerabilityStatus.Unknown;

    public IReadOnlyList<UiPathPackageVulnerability> Vulnerabilities { get; init; } = [];

    public DateTimeOffset? MetadataCheckedAtUtc { get; init; }

    public IReadOnlyList<string> Findings { get; init; } = [];

    public IReadOnlyList<string> UsedActivities { get; init; } = [];

    public IReadOnlyList<string> UsedByWorkflows { get; init; } = [];

    public string? Notes { get; init; }
}

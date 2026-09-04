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

    public IReadOnlyList<string> Findings { get; init; } = [];

    public IReadOnlyList<string> UsedActivities { get; init; } = [];

    public IReadOnlyList<string> UsedByWorkflows { get; init; } = [];

    public string? Notes { get; init; }
}

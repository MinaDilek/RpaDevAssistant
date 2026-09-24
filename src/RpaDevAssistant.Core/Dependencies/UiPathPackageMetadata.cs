namespace RpaDevAssistant.Core.Dependencies;

public enum UiPathPackageMetadataStatus
{
    Available,
    Unknown
}

public enum UiPathPackageDeprecationStatus
{
    NotDeprecated,
    Deprecated,
    Unknown
}

public enum UiPathPackageVulnerabilityStatus
{
    None,
    Known,
    Unknown
}

public sealed record UiPathPackageVulnerability
{
    public int Severity { get; init; }

    public string? AdvisoryUrl { get; init; }
}

public sealed record UiPathPackageMetadata
{
    public UiPathPackageMetadataStatus Status { get; init; } = UiPathPackageMetadataStatus.Unknown;

    public string? LatestVersion { get; init; }

    public UiPathPackageDeprecationStatus DeprecationStatus { get; init; } = UiPathPackageDeprecationStatus.Unknown;

    public IReadOnlyList<string> DeprecationReasons { get; init; } = [];

    public string? AlternatePackage { get; init; }

    public UiPathPackageVulnerabilityStatus VulnerabilityStatus { get; init; } = UiPathPackageVulnerabilityStatus.Unknown;

    public IReadOnlyList<UiPathPackageVulnerability> Vulnerabilities { get; init; } = [];

    public DateTimeOffset? RetrievedAtUtc { get; init; }

    public static UiPathPackageMetadata Unknown { get; } = new();
}

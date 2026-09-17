namespace RpaDevAssistant.Core.Dependencies;

public sealed record UiPathDependencySummary
{
    public int TotalDependencies { get; init; }

    public int UiPathDependencies { get; init; }

    public int ThirdPartyDependencies { get; init; }

    public int UsedDependencies { get; init; }

    public int PossiblyUnusedDependencies { get; init; }

    public int PotentialConflicts { get; init; }

    public int LegacyIndicators { get; init; }

    public int OutdatedDependencies { get; init; }

    public int DeprecatedDependencies { get; init; }

    public int VulnerableDependencies { get; init; }

    public int UnknownMetadataDependencies { get; init; }

    public UiPathModernClassicMode ModernClassicMode { get; init; } = UiPathModernClassicMode.Unknown;

    public IReadOnlyList<UiPathDependencyAnalysis> Packages { get; init; } = [];
}

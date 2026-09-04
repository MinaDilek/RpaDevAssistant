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

    public UiPathModernClassicMode ModernClassicMode { get; init; } = UiPathModernClassicMode.Unknown;

    public IReadOnlyList<UiPathDependencyAnalysis> Packages { get; init; } = [];
}

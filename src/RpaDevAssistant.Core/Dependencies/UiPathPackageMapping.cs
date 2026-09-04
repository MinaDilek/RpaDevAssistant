namespace RpaDevAssistant.Core.Dependencies;

public sealed record UiPathPackageMapping
{
    public required string Family { get; init; }

    public UiPathPackageCategory Category { get; init; } = UiPathPackageCategory.Other;

    public bool IsUiPathPackage { get; init; }

    public bool HasKnownActivityMapping { get; init; }

    public bool IsLegacyIndicator { get; init; }

    public string? Notes { get; init; }
}

public enum UiPathModernClassicSignal
{
    None,
    Modern,
    Classic
}

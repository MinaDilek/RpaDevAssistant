namespace RpaDevAssistant.Core.Dependencies;

public enum UiPathPackageCategory
{
    Core,
    UIAutomation,
    Excel,
    Mail,
    WebAPI,
    Database,
    Orchestrator,
    Credentials,
    DocumentUnderstanding,
    PDF,
    Other
}

public enum UiPathDependencyUsageStatus
{
    Used,
    PossiblyUnused,
    Unknown
}

public enum UiPathDependencyCompatibilityStatus
{
    Compatible,
    PotentialConflict,
    Unknown
}

public enum UiPathDependencyVersionStatus
{
    Current,
    Outdated,
    Legacy,
    Unknown
}

public enum UiPathDependencyRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum UiPathModernClassicMode
{
    Modern,
    Classic,
    Mixed,
    Unknown
}

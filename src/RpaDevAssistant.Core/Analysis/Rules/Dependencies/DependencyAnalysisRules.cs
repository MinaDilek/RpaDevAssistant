using RpaDevAssistant.Core.Dependencies;

namespace RpaDevAssistant.Core.Analysis.Rules;

public sealed class PossiblyUnusedDependencyRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA026";

    public override string Name => "Possibly Unused Dependency";

    public override string Description => "Flags known package families that have no mapped activity usage in parsed workflows.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Configuration;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var package in context.Project.DependencyAnalysis?.Packages ?? [])
        {
            if (package.UsageStatus != UiPathDependencyUsageStatus.PossiblyUnused)
            {
                continue;
            }

            yield return CreateFinding(
                $"{package.Name} may be unused. No parsed activity mapped to this package family.",
                "Review the dependency before removing it. It may still be used indirectly, by Invoke Code, or by custom activities.",
                propertyName: "dependencies",
                currentValue: package.Name);
        }
    }
}

public sealed class PackageVersionAlignmentRiskRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA027";

    public override string Name => "Package Version Alignment Risk";

    public override string Description => "Detects package version alignment, outdated version, and known vulnerability risks.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var package in context.Project.DependencyAnalysis?.Packages ?? [])
        {
            if (package.CompatibilityStatus != UiPathDependencyCompatibilityStatus.PotentialConflict
                && package.VersionStatus != UiPathDependencyVersionStatus.Outdated
                && package.VulnerabilityStatus != UiPathPackageVulnerabilityStatus.Known)
            {
                continue;
            }

            var reasons = new List<string>();
            if (package.CompatibilityStatus == UiPathDependencyCompatibilityStatus.PotentialConflict)
            {
                reasons.Add("declared UiPath package major versions are significantly different");
            }

            if (package.VersionStatus == UiPathDependencyVersionStatus.Outdated)
            {
                reasons.Add($"latest stable NuGet version is {package.LatestVersion}");
            }

            if (package.VulnerabilityStatus == UiPathPackageVulnerabilityStatus.Known)
            {
                reasons.Add($"{package.Vulnerabilities.Count} known vulnerability advisory item(s) affect the declared version");
            }

            yield return CreateFinding(
                $"{package.Name} has package version risk: {string.Join("; ", reasons)}.",
                "Review compatibility and update to a supported non-vulnerable version after testing the workflow in UiPath Studio.",
                propertyName: "dependencies",
                currentValue: $"{package.Name} {package.DeclaredVersion}");
        }
    }
}

public sealed class MixedModernClassicActivityUsageRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA028";

    public override string Name => "Mixed Modern and Classic Activity Usage";

    public override string Description => "Detects projects that contain both modern and classic UiPath activity signals.";

    public override RuleSeverity Severity => RuleSeverity.Info;

    public override RuleCategory Category => RuleCategory.UiAutomation;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        if (context.Project.DependencyAnalysis?.ModernClassicMode != UiPathModernClassicMode.Mixed)
        {
            yield break;
        }

        yield return CreateFinding(
            "Project contains both modern and classic activity usage signals.",
            "Mixed usage is not necessarily wrong. Review this when standardizing or modernizing UI automation patterns.",
            propertyName: "modernClassicMode",
            currentValue: UiPathModernClassicMode.Mixed.ToString());
    }
}

public sealed class LegacyPackageIndicatorRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA029";

    public override string Name => "Legacy Package Indicator";

    public override string Description => "Flags package names with legacy/classic indicators and versions deprecated by the package publisher.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var package in context.Project.DependencyAnalysis?.Packages ?? [])
        {
            if (package.VersionStatus != UiPathDependencyVersionStatus.Legacy
                && package.DeprecationStatus != UiPathPackageDeprecationStatus.Deprecated)
            {
                continue;
            }

            var message = package.DeprecationStatus == UiPathPackageDeprecationStatus.Deprecated
                ? $"{package.Name} {package.DeclaredVersion} is deprecated on NuGet."
                : $"{package.Name} has a legacy/classic package indicator.";
            var recommendation = package.AlternatePackage is null
                ? "Confirm whether this package version is still required and migrate to a supported version where practical."
                : $"Review the publisher's alternate package suggestion ({package.AlternatePackage}) and validate migration in UiPath Studio.";

            yield return CreateFinding(
                message,
                recommendation,
                propertyName: "dependencies",
                currentValue: $"{package.Name} {package.DeclaredVersion}");
        }
    }
}

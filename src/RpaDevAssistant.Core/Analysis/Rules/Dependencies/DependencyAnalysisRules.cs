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

    public override string Description => "Detects significant major version differences among declared UiPath packages using offline heuristics.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var package in context.Project.DependencyAnalysis?.Packages ?? [])
        {
            if (package.CompatibilityStatus != UiPathDependencyCompatibilityStatus.PotentialConflict)
            {
                continue;
            }

            yield return CreateFinding(
                $"{package.Name} may have a package version alignment risk.",
                "Review UiPath package major versions together. This is an offline heuristic, not a latest-version check.",
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

    public override string Description => "Flags package names that contain offline legacy/classic indicators.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var package in context.Project.DependencyAnalysis?.Packages ?? [])
        {
            if (package.VersionStatus != UiPathDependencyVersionStatus.Legacy)
            {
                continue;
            }

            yield return CreateFinding(
                $"{package.Name} has a legacy/classic package indicator.",
                "Confirm whether this package is still required before migration or modernization work.",
                propertyName: "dependencies",
                currentValue: package.Name);
        }
    }
}

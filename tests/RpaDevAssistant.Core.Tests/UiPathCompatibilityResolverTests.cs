using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Rules;
using RpaDevAssistant.Core.Compatibility;
using RpaDevAssistant.Core.Dependencies;
using RpaDevAssistant.Core.Models;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathCompatibilityResolverTests
{
    private readonly UiPathCompatibilityResolver resolver = new();

    [Theory]
    [InlineData("Windows", UiPathModernClassicMode.Modern, UiPathRuntimeCompatibility.Windows, UiPathDesignExperience.Modern, true)]
    [InlineData("Windows", UiPathModernClassicMode.Classic, UiPathRuntimeCompatibility.Windows, UiPathDesignExperience.Classic, true)]
    [InlineData("Windows-Legacy", UiPathModernClassicMode.Unknown, UiPathRuntimeCompatibility.WindowsLegacy, UiPathDesignExperience.Classic, false)]
    [InlineData("Modern", UiPathModernClassicMode.Unknown, UiPathRuntimeCompatibility.Unknown, UiPathDesignExperience.Modern, true)]
    [InlineData("Classic", UiPathModernClassicMode.Unknown, UiPathRuntimeCompatibility.Unknown, UiPathDesignExperience.Classic, false)]
    public void Resolve_ProducesExpectedBehaviorMatrix(
        string compatibility,
        UiPathModernClassicMode mode,
        UiPathRuntimeCompatibility expectedRuntime,
        UiPathDesignExperience expectedExperience,
        bool expectedLegacyFinding)
    {
        var behavior = resolver.Resolve(compatibility, mode);

        Assert.Equal(expectedRuntime, behavior.Runtime);
        Assert.Equal(expectedExperience, behavior.DesignExperience);
        Assert.Equal(expectedLegacyFinding, behavior.FlagLegacyUiActivities);
    }

    [Fact]
    public void LegacyUiRule_RunsForModernWindowsProject()
    {
        var findings = new LegacyUiAutomationActivityRule().Analyze(Context(
            resolver.Resolve("Windows", UiPathModernClassicMode.Modern))).ToArray();

        Assert.Single(findings);
        Assert.Equal("RPA016", findings[0].RuleId);
    }

    [Fact]
    public void LegacyUiRule_IsSuppressedForWindowsLegacyProject()
    {
        var findings = new LegacyUiAutomationActivityRule().Analyze(Context(
            resolver.Resolve("Windows-Legacy", UiPathModernClassicMode.Classic))).ToArray();

        Assert.Empty(findings);
    }

    private static UiPathAnalysisContext Context(UiPathCompatibilityBehavior behavior)
    {
        var workflow = new UiPathWorkflowAnalysis
        {
            FileName = "Main.xaml",
            RelativePath = "Main.xaml"
        };
        workflow.Activities.Add(new UiPathActivityInfo
        {
            ActivityId = "0",
            Name = "OpenBrowser",
            DisplayName = "Open Browser",
            TypeName = "UiPath.Core.Activities.OpenBrowser",
            XamlFile = "Main.xaml"
        });

        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/project",
            ProjectName = "CompatibilityProject",
            CompatibilityBehavior = behavior
        };
        project.Workflows.Add(new UiPathWorkflowInfo
        {
            Name = "Main.xaml",
            RelativePath = "Main.xaml",
            FullPath = "/tmp/project/Main.xaml",
            Analysis = workflow
        });

        return new UiPathAnalysisContext { Project = project };
    }
}

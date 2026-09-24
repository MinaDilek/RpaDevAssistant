using RpaDevAssistant.Api.Responses;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.Localization;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class BackendLocalizationTests
{
    [Fact]
    public void FindingLocalizer_ReturnsTurkishRuleTextWithoutChangingCanonicalFields()
    {
        var result = new UiPathStaticAnalysisResult();
        result.Findings.Add(Finding("RPA003", RuleSeverity.Error));

        var localized = new UiPathAnalysisFindingLocalizer(new RpaDevAssistantLocalizer()).Localize(result, "tr");
        var finding = Assert.Single(localized.Findings);

        Assert.Equal("RPA003", finding.RuleId);
        Assert.Equal(RuleSeverity.Error, finding.Severity);
        Assert.Equal("Exception sessizce yutuluyor", finding.RuleName);
        Assert.Contains("Catch bloğunda", finding.Message);
    }

    [Fact]
    public void FindingLocalizer_ReturnsEnglishRuleText()
    {
        var localized = new UiPathAnalysisFindingLocalizer(new RpaDevAssistantLocalizer()).Localize(Finding("RPA003", RuleSeverity.Error), "en");

        Assert.Equal("Exception Silently Swallowed", localized.RuleName);
        Assert.Contains("Catch block", localized.Message);
    }

    [Fact]
    public void AnalyzeResponse_LocalizesFindingsButKeepsScoringStable()
    {
        var analysis = new UiPathStaticAnalysisResult();
        analysis.Findings.Add(Finding("RPA003", RuleSeverity.Error));
        var result = new UiPathProjectAnalysisResult
        {
            ProjectScan = new RpaDevAssistant.Core.Models.ProjectScanResult { ProjectPath = "/tmp/project", ProjectName = "Project" },
            Analysis = analysis,
            QualityScore = new UiPathQualityScore { Score = 80, Grade = "B", ProfileId = "default", ProfileName = "Default", TotalFindings = 1 },
            Profile = new RpaDevAssistant.Core.Analysis.Profiles.UiPathRuleProfile { Id = "default", Name = "Default" }
        };

        var response = AnalyzeUiPathProjectResponse.From(result, new UiPathAnalysisFindingLocalizer(new RpaDevAssistantLocalizer()), "tr");

        Assert.Equal("tr", response.Locale);
        Assert.Equal(80, response.QualityScore?.Score);
        Assert.Equal("Exception sessizce yutuluyor", response.Analysis.Findings[0].RuleName);
    }

    [Fact]
    public void FixSuggestionLocalizer_ReturnsTurkishAndEnglishText()
    {
        var localizer = new UiPathFixSuggestionLocalizer(new RpaDevAssistantLocalizer());
        var suggestion = new UiPathFixSuggestion
        {
            RuleId = "RPA007",
            Title = "Rename the activity DisplayName.",
            Description = "Use a DisplayName that explains the business action.",
            Explanation = "Specific DisplayName values make workflows easier to review and debug.",
            FixType = UiPathFixSuggestionType.NamingChange,
            Confidence = UiPathFixConfidence.High,
            RiskLevel = UiPathFixRiskLevel.Low,
            Steps = ["Review the proposed DisplayName."],
            Risks = ["Low risk: DisplayName is designer metadata and should not affect runtime behavior."],
            ValidationNotes = ["Review the suggested name in context before changing it in UiPath Studio."],
            PatchPreview = new UiPathPatchPreview
            {
                Format = UiPathPatchPreviewFormat.Text,
                Description = "Instruction-only preview. No XAML patch is generated.",
                Before = "DisplayName = Click",
                After = "DisplayName = Click Login Button",
                Notes = ["Review and apply manually in UiPath Studio."]
            }
        };

        var turkish = localizer.Localize(suggestion, "tr");
        Assert.Contains("DisplayName", turkish.Title);
        Assert.Contains("Önerilen DisplayName", turkish.Steps[0]);
        Assert.Contains("Düşük risk", turkish.Risks[0]);
        Assert.Equal("Sadece talimat önizlemesi. XAML patch üretilmez.", turkish.PatchPreview?.Description);
        Assert.Equal("UiPath Studio içinde manuel olarak gözden geçirip uygulayın.", turkish.PatchPreview?.Notes[0]);
        Assert.Contains("activity", localizer.Localize(suggestion, "en").Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Business action", turkish.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingKey_FallsBackToEnglishFallbackWithoutShowingKey()
    {
        var value = new RpaDevAssistantLocalizer().Get("Missing.Key", "tr", fallback: "English fallback");

        Assert.Equal("English fallback", value);
    }

    [Fact]
    public void BuiltInRules_HaveRequiredTranslationsForBothLocales()
    {
        var localizer = new RpaDevAssistantLocalizer();
        foreach (var locale in new[] { "en", "tr" })
        {
            for (var index = 1; index <= 46; index++)
            {
                var ruleId = $"RPA{index:000}";
                Assert.False(string.IsNullOrWhiteSpace(localizer.Get($"Rules.{ruleId}.Name", locale)));
                Assert.False(string.IsNullOrWhiteSpace(localizer.Get($"Rules.{ruleId}.Message", locale)));
                Assert.False(string.IsNullOrWhiteSpace(localizer.Get($"Rules.{ruleId}.Recommendation", locale)));
            }
        }
    }

    private static UiPathAnalysisFinding Finding(string ruleId, RuleSeverity severity)
    {
        return new UiPathAnalysisFinding
        {
            RuleId = ruleId,
            RuleName = "English Rule",
            Severity = severity,
            Category = RuleCategory.ExceptionHandling,
            Message = "English message",
            Description = "English description",
            Recommendation = "English recommendation",
            WorkflowPath = "Main.xaml"
        };
    }
}

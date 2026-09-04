using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Models;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathAiReviewTests
{
    [Fact]
    public void ContextBuilder_IncludesProjectMetadata()
    {
        var request = BuildContext();

        Assert.Equal("InvoiceAutomation", request.ProjectName);
        Assert.Equal("Windows", request.Compatibility);
        Assert.Equal(2, request.WorkflowCount);
    }

    [Fact]
    public void ContextBuilder_WorkflowScopeIncludesOnlySelectedWorkflow()
    {
        var request = BuildContext(scope: UiPathAiReviewScope.Workflow, workflowPath: "Business/Process.xaml");

        var workflow = Assert.Single(request.Workflows);
        Assert.Equal("Business/Process.xaml", workflow.RelativePath);
    }

    [Fact]
    public void SecretRedactor_RedactsPassword()
    {
        Assert.Equal("[REDACTED]", new SensitiveValueRedactor().Redact("Password", "abc"));
    }

    [Fact]
    public void SecretRedactor_RedactsToken()
    {
        Assert.Equal("[REDACTED]", new SensitiveValueRedactor().Redact("access_token", "abc"));
    }

    [Fact]
    public void SecretRedactor_KeepsNormalProperty()
    {
        Assert.Equal("Login", new SensitiveValueRedactor().Redact("DisplayName", "Login"));
    }

    [Fact]
    public void ContextBuilder_AppliesMaxFindingsLimit()
    {
        var request = BuildContext(options: new UiPathAiReviewOptions { MaxFindings = 1 });

        Assert.Single(request.DeterministicFindings);
    }

    [Fact]
    public void ContextBuilder_AppliesMaxActivitiesLimit()
    {
        var request = BuildContext(options: new UiPathAiReviewOptions { MaxActivitiesPerWorkflow = 1 });

        Assert.All(request.Workflows, workflow => Assert.True(workflow.Activities.Count <= 1));
    }

    [Fact]
    public void Prompt_IncludesDeterministicFindings()
    {
        var prompt = new UiPathAiPromptBuilder().Build(BuildContext());

        Assert.Contains("Deterministic Findings", prompt.UserContext, StringComparison.Ordinal);
        Assert.Contains("RPA001", prompt.UserContext, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_IncludesHallucinationControlInstructions()
    {
        var prompt = new UiPathAiPromptBuilder().Build(BuildContext());

        Assert.Contains("Do not invent activities", prompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("If evidence is insufficient", prompt.SystemInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_IncludesTurkishLanguageInstruction()
    {
        var prompt = new UiPathAiPromptBuilder().Build(BuildContext(locale: "tr"));

        Assert.Contains("Respond in Turkish", prompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("identifiers unchanged", prompt.SystemInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_IncludesEnglishLanguageInstruction()
    {
        var prompt = new UiPathAiPromptBuilder().Build(BuildContext(locale: "en"));

        Assert.Contains("Respond in English", prompt.SystemInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReviewService_ReturnsNotConfigured_WhenProviderHasNoApiKey()
    {
        var service = new UiPathAiReviewService(
            new FakeAnalyzer(),
            new UiPathAiReviewContextBuilder(new SensitiveValueRedactor()),
            new UiPathAiPromptBuilder(),
            new FakeProvider(isConfigured: false),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UiPathAiReviewService>.Instance);

        var result = await service.ReviewAsync("/tmp/project", "default", UiPathAiReviewScope.Project, null, null, CancellationToken.None);

        Assert.False(result.IsConfigured);
        Assert.Equal("AI Review is not configured.", result.ErrorMessage);
    }

    [Fact]
    public async Task ReviewService_ValidatesWorkflowPath()
    {
        var service = new UiPathAiReviewService(
            new FakeAnalyzer(),
            new UiPathAiReviewContextBuilder(new SensitiveValueRedactor()),
            new UiPathAiPromptBuilder(),
            new FakeProvider(isConfigured: true),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UiPathAiReviewService>.Instance);

        var result = await service.ReviewAsync("/tmp/project", "default", UiPathAiReviewScope.Workflow, null, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("workflowPath", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReviewService_ProviderExceptionDoesNotBreakDeterministicAnalysis()
    {
        var service = new UiPathAiReviewService(
            new FakeAnalyzer(),
            new UiPathAiReviewContextBuilder(new SensitiveValueRedactor()),
            new UiPathAiPromptBuilder(),
            new ThrowingProvider(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UiPathAiReviewService>.Instance);

        var result = await service.ReviewAsync("/tmp/project", "default", UiPathAiReviewScope.Project, null, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("AI review could not be completed.", result.Summary);
    }

    private static UiPathAiReviewRequest BuildContext(UiPathAiReviewScope scope = UiPathAiReviewScope.Project, string? workflowPath = null, UiPathAiReviewOptions? options = null, string? locale = null)
    {
        return new UiPathAiReviewContextBuilder(new SensitiveValueRedactor(), options).Build(Project(), Analysis(), Score(), scope, workflowPath, locale: locale);
    }

    private static ProjectScanResult Project()
    {
        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/project",
            ProjectName = "InvoiceAutomation",
            Compatibility = "Windows",
            ProjectFolderExists = true,
            ProjectJsonExists = true,
            ProjectJsonParsed = true
        };
        project.Workflows.Add(Workflow("Main.xaml"));
        project.Workflows.Add(Workflow("Business/Process.xaml"));
        project.Dependencies.Add(new UiPathDependency { Name = "UiPath.System.Activities", Version = "23.10.1" });
        return project;
    }

    private static UiPathWorkflowInfo Workflow(string relativePath)
    {
        var analysis = new UiPathWorkflowAnalysis
        {
            FileName = Path.GetFileName(relativePath),
            RelativePath = relativePath
        };
        analysis.Activities.Add(new UiPathActivityInfo
        {
            ActivityId = $"{relativePath}-1",
            Name = "Sequence",
            DisplayName = "Sequence",
            TypeName = "Sequence",
            Depth = 0,
            XamlFile = relativePath
        });
        analysis.Activities.Add(new UiPathActivityInfo
        {
            ActivityId = $"{relativePath}-2",
            ParentActivityId = $"{relativePath}-1",
            Name = "HTTPRequest",
            DisplayName = "Send Request",
            TypeName = "HTTPRequest",
            Depth = 1,
            XamlFile = relativePath,
            Properties = new Dictionary<string, string?> { ["ApiKey"] = "secret", ["Endpoint"] = "https://example.test" }
        });
        return new UiPathWorkflowInfo
        {
            Name = Path.GetFileName(relativePath),
            RelativePath = relativePath,
            FullPath = $"/tmp/project/{relativePath}",
            Analysis = analysis
        };
    }

    private static UiPathStaticAnalysisResult Analysis()
    {
        var result = new UiPathStaticAnalysisResult();
        result.Findings.Add(new UiPathAnalysisFinding
        {
            RuleId = "RPA001",
            RuleName = "Avoid Delay Activities",
            Severity = RuleSeverity.Warning,
            Category = RuleCategory.Reliability,
            Message = "Delay activity detected.",
            WorkflowPath = "Main.xaml"
        });
        result.Findings.Add(new UiPathAnalysisFinding
        {
            RuleId = "RPA007",
            RuleName = "Generic Activity Display Name",
            Severity = RuleSeverity.Suggestion,
            Category = RuleCategory.Maintainability,
            Message = "Generic name.",
            WorkflowPath = "Business/Process.xaml"
        });
        return result;
    }

    private static UiPathQualityScore Score()
    {
        return new UiPathQualityScore
        {
            Score = 88,
            Grade = "B",
            ProfileId = "default",
            ProfileName = "Default"
        };
    }

    private sealed class FakeAnalyzer : IUiPathProjectAnalyzer
    {
        public UiPathProjectAnalysisResult Analyze(string projectPath, string? profileId = null)
        {
            return new UiPathProjectAnalysisResult
            {
                ProjectScan = Project(),
                Analysis = Analysis(),
                QualityScore = Score(),
                Profile = new UiPathRuleProfile { Id = "default", Name = "Default" }
            };
        }
    }

    private sealed class FakeProvider : IUiPathAiReviewProvider
    {
        public FakeProvider(bool isConfigured)
        {
            IsConfigured = isConfigured;
        }

        public string ProviderName => "Fake";

        public bool IsConfigured { get; }

        public Task<UiPathAiReviewResult> ReviewAsync(UiPathAiPrompt prompt, CancellationToken cancellationToken)
        {
            return Task.FromResult(new UiPathAiReviewResult
            {
                Summary = "Looks reasonable.",
                RiskLevel = UiPathAiRiskLevel.Low,
                ReviewedScope = prompt.Scope,
                Confidence = 0.8
            });
        }
    }

    private sealed class ThrowingProvider : IUiPathAiReviewProvider
    {
        public string ProviderName => "Throwing";

        public bool IsConfigured => true;

        public Task<UiPathAiReviewResult> ReviewAsync(UiPathAiPrompt prompt, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("boom");
        }
    }
}

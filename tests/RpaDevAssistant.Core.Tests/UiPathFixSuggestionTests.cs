using Microsoft.Extensions.Logging.Abstractions;
using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.Fixes.Providers;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.ProjectAssistant;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathFixSuggestionTests
{
    [Fact]
    public async Task Rpa001_Delay_ReturnsStateBasedWaitSuggestion()
    {
        var result = await Service(Project("RPA001")).SuggestAsync(Request("RPA001"), CancellationToken.None);

        Assert.NotNull(result.Suggestion);
        Assert.Equal(UiPathFixSuggestionType.ActivityReplacement, result.Suggestion.FixType);
        Assert.Contains("Check App State", result.Suggestion.AfterPreview);
    }

    [Fact]
    public async Task Rpa004_LongDelay_ReturnsReplacementSuggestion()
    {
        var result = await Service(Project("RPA004")).SuggestAsync(Request("RPA004"), CancellationToken.None);

        Assert.NotNull(result.Suggestion);
        Assert.Equal(UiPathFixSuggestionType.ActivityReplacement, result.Suggestion.FixType);
        Assert.Equal(UiPathFixRiskLevel.Medium, result.Suggestion.RiskLevel);
    }

    [Fact]
    public async Task Rpa006_WorkflowNaming_ReturnsGenericNamingSuggestion()
    {
        var result = await Service(Project("RPA006")).SuggestAsync(Request("RPA006", workflowPath: "workflow1.xaml"), CancellationToken.None);

        Assert.NotNull(result.Suggestion);
        Assert.Equal(UiPathFixSuggestionType.NamingChange, result.Suggestion.FixType);
        Assert.Contains("PascalCase", result.Suggestion.SuggestedValue);
    }

    [Fact]
    public async Task Rpa007_GenericClickDisplayName_ReturnsBetterDisplayName()
    {
        var result = await Service(Project("RPA007")).SuggestAsync(Request("RPA007"), CancellationToken.None);

        Assert.NotNull(result.Suggestion);
        Assert.Equal("Click Login", result.Suggestion.SuggestedValue);
        Assert.Equal(UiPathFixability.SafeAutomatic, result.Suggestion.Fixability);
        Assert.Equal(UiPathPatchPreviewFormat.PropertyChange, result.Suggestion.PatchPreview?.Format);
    }

    [Fact]
    public async Task Rpa007_AggregatedFindingWithoutActivityLocator_ReturnsPreviewableManualSuggestion()
    {
        var result = await Service(Project("RPA007_AGGREGATED")).SuggestAsync(Request("RPA007"), CancellationToken.None);

        Assert.NotNull(result.Suggestion);
        Assert.False(result.Suggestion.CanAutoApply);
        Assert.Equal(UiPathFixability.Previewable, result.Suggestion.Fixability);
        Assert.Equal(UiPathPatchPreviewFormat.InstructionOnly, result.Suggestion.PatchPreview?.Format);
        Assert.Contains("Click", result.Suggestion.BeforePreview);
        Assert.Contains(result.Suggestion.ValidationNotes, note => note.Contains("Auto-apply is only available", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Rpa007_AggregatedFindingWithActivityLocator_ReturnsSafeAutomaticPropertySuggestion()
    {
        var result = await Service(Project("RPA007_AGGREGATED")).SuggestAsync(Request("RPA007", activityId: "click-1"), CancellationToken.None);

        Assert.NotNull(result.Suggestion);
        Assert.True(result.Suggestion.CanAutoApply);
        Assert.Equal("Click Login", result.Suggestion.SuggestedValue);
        Assert.Equal(UiPathPatchPreviewFormat.PropertyChange, result.Suggestion.PatchPreview?.Format);
    }

    [Fact]
    public async Task Rpa008_MissingLogging_ReturnsInsertionRecommendation()
    {
        var result = await Service(Project("RPA008")).SuggestAsync(Request("RPA008", workflowPath: "Main.xaml"), CancellationToken.None);

        Assert.NotNull(result.Suggestion);
        Assert.Equal(UiPathFixSuggestionType.ActivityInsertion, result.Suggestion.FixType);
        Assert.Contains("Log Message", result.Suggestion.AfterPreview);
    }

    [Fact]
    public async Task Rpa002_EmptyCatch_ReturnsHighRiskHandlingSuggestion()
    {
        var result = await Service(Project("RPA002")).SuggestAsync(Request("RPA002"), CancellationToken.None);

        Assert.NotNull(result.Suggestion);
        Assert.Equal(UiPathFixRiskLevel.High, result.Suggestion.RiskLevel);
        Assert.Contains("Rethrow", result.Suggestion.AfterPreview);
    }

    [Fact]
    public async Task Rpa003_SilentCatch_ReturnsLogAndRethrowRecommendation()
    {
        var result = await Service(Project("RPA003")).SuggestAsync(Request("RPA003"), CancellationToken.None);

        Assert.NotNull(result.Suggestion);
        Assert.Equal(UiPathFixSuggestionType.ExceptionHandlingChange, result.Suggestion.FixType);
        Assert.Contains("Log Message", result.Suggestion.AfterPreview);
    }

    [Fact]
    public async Task Rpa005_MissingInvokeWithSimilarWorkflow_ReturnsCandidateSuggestion()
    {
        var result = await Service(Project("RPA005")).SuggestAsync(Request("RPA005"), CancellationToken.None);

        Assert.NotNull(result.Suggestion);
        Assert.Equal("Framework/SetTransactionState.xaml", result.Suggestion.SuggestedValue);
    }

    [Fact]
    public void Similarity_ReturnsHighScoreForCloseFilenames()
    {
        var score = FileNameSimilarity.Calculate("SetTransactionStatus.xaml", "SetTransactionState.xaml");

        Assert.True(score >= FileNameSimilarity.HighConfidenceThreshold);
    }

    [Fact]
    public void Similarity_ReturnsLowScoreForDifferentFilenames()
    {
        var score = FileNameSimilarity.Calculate("SetTransactionStatus.xaml", "Login.xaml");

        Assert.True(score < FileNameSimilarity.HighConfidenceThreshold);
    }

    [Fact]
    public void Similarity_DoesNotSuggestBelowThreshold()
    {
        var suggestion = FileNameSimilarity.FindSimilar("SetTransactionStatus.xaml", ["Login.xaml"]);

        Assert.Null(suggestion);
    }

    [Fact]
    public void Validator_FailsWhenWorkflowMissing()
    {
        var context = Context(Project("RPA007"), Finding("RPA007", workflowPath: "Missing.xaml"));
        var suggestion = Suggestion("RPA007") with { WorkflowPath = "Missing.xaml", FixType = UiPathFixSuggestionType.NamingChange, PropertyName = "DisplayName", CurrentValue = "Click", SuggestedValue = "Click Login" };

        var result = new UiPathFixSuggestionValidator().Validate(context, suggestion);

        Assert.Contains(result.Errors, error => error.Contains("Workflow path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_FailsWhenActivityMissing()
    {
        var context = Context(Project("RPA007"), Finding("RPA007", workflowPath: "Main.xaml") with { ActivityId = "missing" });
        var suggestion = Suggestion("RPA007") with { WorkflowPath = "Main.xaml", ActivityId = "missing", FixType = UiPathFixSuggestionType.NamingChange, PropertyName = "DisplayName", CurrentValue = "Click", SuggestedValue = "Click Login" };

        var result = new UiPathFixSuggestionValidator().Validate(context, suggestion);

        Assert.Contains(result.Errors, error => error.Contains("Activity id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_FailsWhenCurrentPropertyChanged()
    {
        var project = Project("RPA007");
        var finding = project.Analysis.Findings.Single();
        var context = Context(project, finding);
        var suggestion = Suggestion("RPA007") with { WorkflowPath = "Main.xaml", ActivityId = finding.ActivityId, FixType = UiPathFixSuggestionType.NamingChange, PropertyName = "DisplayName", CurrentValue = "Old Click", SuggestedValue = "Click Login" };

        var result = new UiPathFixSuggestionValidator().Validate(context, suggestion);

        Assert.Contains(result.Errors, error => error.Contains("Current property value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_FailsWhenSuggestedValueEmpty()
    {
        var project = Project("RPA007");
        var context = Context(project, project.Analysis.Findings.Single());
        var suggestion = Suggestion("RPA007") with { WorkflowPath = "Main.xaml", FixType = UiPathFixSuggestionType.NamingChange, PropertyName = "DisplayName", CurrentValue = "Click", SuggestedValue = "" };

        var result = new UiPathFixSuggestionValidator().Validate(context, suggestion);

        Assert.Contains(result.Errors, error => error.Contains("cannot be empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_FailsStaleRuleMismatch()
    {
        var project = Project("RPA007");
        var context = Context(project, project.Analysis.Findings.Single());
        var suggestion = Suggestion("RPA001") with { WorkflowPath = "Main.xaml", FixType = UiPathFixSuggestionType.ActivityReplacement };

        var result = new UiPathFixSuggestionValidator().Validate(context, suggestion);

        Assert.Contains(result.Errors, error => error.Contains("rule id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AiFix_CallsProviderWhenNoDeterministicProviderAndExplicitAiRequested()
    {
        var advisor = new FakeAiFixAdvisor { IsConfiguredValue = true };
        var result = await Service(Project("RPA999"), advisor).SuggestAsync(Request("RPA999", useAi: true), CancellationToken.None);

        Assert.True(advisor.WasCalled);
        Assert.NotNull(result.Suggestion);
        Assert.True(result.Suggestion.RequiresAi);
    }

    [Fact]
    public async Task AiFix_ContextIsMinimizedAndSecretsRedacted()
    {
        var advisor = new FakeAiFixAdvisor { IsConfiguredValue = true };
        await Service(Project("RPA999"), advisor).SuggestAsync(Request("RPA999", useAi: true), CancellationToken.None);

        Assert.Contains("NearbyActivities", advisor.LastPrompt?.UserContext);
        Assert.Contains("[REDACTED]", advisor.LastPrompt?.UserContext);
        Assert.DoesNotContain("super-secret", advisor.LastPrompt?.UserContext);
    }

    [Fact]
    public void AiFix_MapsStructuredProviderResponse()
    {
        var payload = """
            {
              "output_text": "{\"title\":\"Refactor handling\",\"description\":\"Use a safer flow\",\"fixType\":\"WorkflowRefactor\",\"riskLevel\":\"Medium\",\"confidence\":\"Medium\",\"beforePreview\":\"Before\",\"afterPreview\":\"After\",\"explanation\":\"Evidence-based suggestion\",\"validationNotes\":[\"Test in UiPath Studio\"]}"
            }
            """;

        var suggestion = RpaDevAssistant.Infrastructure.OpenAI.OpenAiUiPathFixAdvisor.ParseResponsePayload(payload);

        Assert.Equal("Refactor handling", suggestion.Title);
        Assert.True(suggestion.RequiresAi);
        Assert.False(suggestion.CanAutoApply);
    }

    [Fact]
    public void AiFix_InvalidResponseReturnsErrorSuggestion()
    {
        var suggestion = RpaDevAssistant.Infrastructure.OpenAI.OpenAiUiPathFixAdvisor.ParseResponsePayload("{ invalid");

        Assert.NotNull(suggestion.ErrorMessage);
        Assert.Equal(UiPathFixConfidence.Low, suggestion.Confidence);
    }

    [Fact]
    public async Task AiFix_ReturnsUnavailableWhenProviderNotConfigured()
    {
        var result = await Service(Project("RPA999"), new FakeAiFixAdvisor { IsConfiguredValue = false }).SuggestAsync(Request("RPA999", useAi: true), CancellationToken.None);

        Assert.Equal("AI-assisted fix suggestions are not configured.", result.Message);
    }

    private static UiPathFixSuggestionService Service(UiPathProjectAnalysisResult project, FakeAiFixAdvisor? advisor = null)
    {
        IUiPathFixSuggestionProvider[] providers =
        [
            new DelayFixSuggestionProvider(),
            new WorkflowNamingFixSuggestionProvider(),
            new DisplayNameFixSuggestionProvider(),
            new MissingLoggingFixSuggestionProvider(),
            new ExceptionHandlingFixSuggestionProvider(),
            new InvalidInvokeWorkflowFixSuggestionProvider()
        ];

        var graphBuilder = new UiPathWorkflowGraphBuilder();
        return new UiPathFixSuggestionService(
            new FakeAnalyzer(project),
            new UiPathFixSuggestionRegistry(providers),
            new UiPathFixContextBuilder(graphBuilder),
            new UiPathFixSuggestionValidator(),
            new UiPathAiFixPromptBuilder(new SensitiveValueRedactor()),
            advisor ?? new FakeAiFixAdvisor(),
            NullLogger<UiPathFixSuggestionService>.Instance);
    }

    private static UiPathFixSuggestionRequest Request(string ruleId, string workflowPath = "Main.xaml", bool useAi = false, string? activityId = null)
    {
        return new UiPathFixSuggestionRequest
        {
            ProjectPath = "/tmp/project",
            RuleId = ruleId,
            WorkflowPath = workflowPath,
            UseAi = useAi,
            ActivityId = activityId
        };
    }

    private static UiPathFixContext Context(UiPathProjectAnalysisResult project, UiPathAnalysisFinding finding)
    {
        return new UiPathFixContextBuilder(new UiPathWorkflowGraphBuilder()).Build(project, finding);
    }

    private static UiPathProjectAnalysisResult Project(string ruleId)
    {
        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/project",
            ProjectName = "InvoiceAutomation",
            ProjectFolderExists = true,
            ProjectJsonExists = true,
            ProjectJsonParsed = true
        };
        var clickId = "click-1";
        var delayId = "delay-1";
        var invokeId = "invoke-1";
        project.Workflows.Add(Workflow("Main.xaml",
            Activity("Sequence", "Main", "seq-1"),
            Activity("Click", "Click", clickId, ("Target", "btnLogin"), ("Password", "super-secret")),
            Activity("Delay", "Delay", delayId, ("Duration", "00:00:05")),
            Activity("InvokeWorkflowFile", "Invoke Missing", invokeId, ("WorkflowFileName", "Framework/SetTransactionStatus.xaml"))));
        project.Workflows.Add(Workflow("Framework/SetTransactionState.xaml", Activity("Sequence", "Set Transaction State", "seq-2")));
        project.Workflows.Add(Workflow("workflow1.xaml", Activity("Sequence", "workflow1", "seq-3")));

        var finding = ruleId switch
        {
            "RPA001" => Finding(ruleId, workflowPath: "Main.xaml", activityId: delayId, activityName: "Delay", displayName: "Delay", propertyName: "Duration", currentValue: "00:00:05"),
            "RPA004" => Finding(ruleId, workflowPath: "Main.xaml", activityId: delayId, activityName: "Delay", displayName: "Delay", propertyName: "Duration", currentValue: "00:00:05"),
            "RPA005" => Finding(ruleId, workflowPath: "Main.xaml", activityId: invokeId, activityName: "InvokeWorkflowFile", displayName: "Invoke Missing", propertyName: "WorkflowFileName", currentValue: "Framework/SetTransactionStatus.xaml"),
            "RPA006" => Finding(ruleId, workflowPath: "workflow1.xaml"),
            "RPA007" => Finding(ruleId, workflowPath: "Main.xaml", activityId: clickId, activityName: "Click", displayName: "Click", propertyName: "DisplayName", currentValue: "Click"),
            "RPA007_AGGREGATED" => Finding("RPA007", workflowPath: "Main.xaml") with
            {
                Scope = UiPathFindingScope.Aggregated,
                OccurrenceCount = 2,
                AffectedActivityCount = 2,
                TotalRelevantActivityCount = 2,
                Percentage = 100,
                AffectedActivities =
                [
                    new UiPathAffectedActivity
                    {
                        ActivityId = clickId,
                        StableId = clickId,
                        ActivityPath = "0/1",
                        ActivityName = "Click",
                        ActivityDisplayName = "Click",
                        PropertyName = "DisplayName",
                        CurrentValue = "Click"
                    },
                    new UiPathAffectedActivity
                    {
                        ActivityId = "assign-1",
                        StableId = "assign-1",
                        ActivityPath = "0/2",
                        ActivityName = "Assign",
                        ActivityDisplayName = "Assign",
                        PropertyName = "DisplayName",
                        CurrentValue = "Assign"
                    }
                ],
                ExampleActivities =
                [
                    new UiPathAffectedActivity
                    {
                        ActivityId = clickId,
                        StableId = clickId,
                        ActivityPath = "0/1",
                        ActivityName = "Click",
                        ActivityDisplayName = "Click",
                        PropertyName = "DisplayName",
                        CurrentValue = "Click"
                    }
                ]
            },
            "RPA008" => Finding(ruleId, workflowPath: "Main.xaml"),
            "RPA002" => Finding(ruleId, workflowPath: "Main.xaml", activityId: "catch-1", activityName: "Catch", displayName: "Catch"),
            "RPA003" => Finding(ruleId, workflowPath: "Main.xaml", activityId: "catch-1", activityName: "Catch", displayName: "Catch"),
            _ => Finding(ruleId, workflowPath: "Main.xaml", activityId: clickId, activityName: "Click", displayName: "Click", propertyName: "Password", currentValue: "super-secret")
        };

        var analysis = new UiPathStaticAnalysisResult();
        analysis.Findings.Add(finding);

        return new UiPathProjectAnalysisResult
        {
            ProjectScan = project,
            Analysis = analysis,
            QualityScore = new UiPathQualityScore { Score = 80, Grade = "B", ProfileId = "default", ProfileName = "Default" },
            Profile = new UiPathRuleProfile { Id = "default", Name = "Default" }
        };
    }

    private static UiPathWorkflowInfo Workflow(string path, params UiPathActivityInfo[] activities)
    {
        var analysis = new UiPathWorkflowAnalysis { FileName = Path.GetFileName(path), RelativePath = path };
        analysis.Activities.AddRange(activities.Select(activity => activity with { XamlFile = path }));
        return new UiPathWorkflowInfo { Name = Path.GetFileName(path), RelativePath = path, FullPath = $"/tmp/project/{path}", Analysis = analysis };
    }

    private static UiPathActivityInfo Activity(string name, string displayName, string id, params (string Key, string Value)[] properties)
    {
        return new UiPathActivityInfo
        {
            ActivityId = id,
            Name = name,
            DisplayName = displayName,
            TypeName = name,
            XamlFile = "Main.xaml",
            Properties = properties.ToDictionary(property => property.Key, property => (string?)property.Value)
        };
    }

    private static UiPathAnalysisFinding Finding(
        string ruleId,
        string workflowPath,
        string? activityId = null,
        string? activityName = null,
        string? displayName = null,
        string? propertyName = null,
        string? currentValue = null)
    {
        return new UiPathAnalysisFinding
        {
            RuleId = ruleId,
            RuleName = ruleId,
            Severity = RuleSeverity.Warning,
            Category = RuleCategory.Maintainability,
            Message = $"{ruleId} message",
            WorkflowPath = workflowPath,
            ActivityId = activityId,
            ActivityName = activityName,
            ActivityDisplayName = displayName,
            PropertyName = propertyName,
            CurrentValue = currentValue
        };
    }

    private static UiPathFixSuggestion Suggestion(string ruleId)
    {
        return new UiPathFixSuggestion
        {
            Id = "fix",
            RuleId = ruleId,
            Title = "Fix",
            Description = "Fix",
            Explanation = "Fix",
            Confidence = UiPathFixConfidence.Medium,
            RiskLevel = UiPathFixRiskLevel.Low
        };
    }

    private sealed class FakeAnalyzer : IUiPathProjectAnalyzer
    {
        private readonly UiPathProjectAnalysisResult result;

        public FakeAnalyzer(UiPathProjectAnalysisResult result)
        {
            this.result = result;
        }

        public UiPathProjectAnalysisResult Analyze(string projectPath, string? profileId = null)
        {
            return result;
        }
    }

    private sealed class FakeAiFixAdvisor : IUiPathAiFixAdvisor
    {
        public bool IsConfiguredValue { get; init; }

        public bool WasCalled { get; private set; }

        public UiPathAiFixPrompt? LastPrompt { get; private set; }

        public string ProviderName => "Fake";

        public bool IsConfigured => IsConfiguredValue;

        public Task<UiPathFixSuggestion> SuggestAsync(UiPathAiFixPrompt prompt, CancellationToken cancellationToken)
        {
            WasCalled = true;
            LastPrompt = prompt;
            return Task.FromResult(new UiPathFixSuggestion
            {
                Title = "AI fix",
                Description = "AI description",
                FixType = UiPathFixSuggestionType.WorkflowRefactor,
                Confidence = UiPathFixConfidence.Medium,
                RiskLevel = UiPathFixRiskLevel.Medium,
                BeforePreview = "Before",
                AfterPreview = "After",
                Explanation = "AI explanation",
                RequiresAi = true
            });
        }
    }
}

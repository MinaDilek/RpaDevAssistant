using Microsoft.Extensions.Logging.Abstractions;
using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.CustomRules;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.RuleCatalog;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.History;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.ProjectAssistant;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathProjectQuestionServiceTests
{
    [Fact]
    public void Retriever_FindsExactActivityWorkflow()
    {
        var evidence = Retriever().Retrieve(Project(), Question("Delay nerede kullanılıyor?"));

        Assert.Contains(evidence, item => item.Type == UiPathProjectEvidenceType.Activity && item.WorkflowPath == "Main.xaml" && item.ActivityName == "Delay");
    }

    [Fact]
    public void Retriever_NormalizesActivityAliases()
    {
        var evidence = Retriever().Retrieve(Project(), Question("Where is Get Credential used?"));

        Assert.Contains(evidence, item => item.ActivityName == "GetCredential" && item.WorkflowPath == "Framework/Process.xaml");
    }

    [Fact]
    public void Retriever_TreatsHttpRequestAliasesAsSameActivity()
    {
        var evidence = Retriever().Retrieve(Project(), Question("http request nerede kullanılıyor?"));

        Assert.Contains(evidence, item => item.ActivityName == "HTTPRequest" && item.WorkflowPath == "Business/SendMail.xaml");
    }

    [Fact]
    public void Retriever_FindsWorkflowByName()
    {
        var evidence = Retriever().Retrieve(Project(), Question("Explain Process.xaml"));

        Assert.Contains(evidence, item => item.Type == UiPathProjectEvidenceType.Workflow && item.WorkflowPath == "Framework/Process.xaml");
    }

    [Fact]
    public void Retriever_FindsRuleId()
    {
        var evidence = Retriever().Retrieve(Project(), Question("RPA003 nerede?"));

        Assert.Contains(evidence, item => item.Type == UiPathProjectEvidenceType.Finding && item.RuleId == "RPA003");
    }

    [Fact]
    public void Retriever_FindsFindingMessage()
    {
        var evidence = Retriever().Retrieve(Project(), Question("silently swallowed exception"));

        Assert.Contains(evidence, item => item.Type == UiPathProjectEvidenceType.Finding && item.RuleId == "RPA003");
    }

    [Fact]
    public void Retriever_OrdersByRelevance()
    {
        var evidence = Retriever().Retrieve(Project(), Question("Delay"));

        Assert.Equal("Delay", evidence[0].ActivityName);
    }

    [Fact]
    public void Retriever_AppliesMaxEvidenceItems()
    {
        var evidence = Retriever().Retrieve(Project(), Question("workflow", maxEvidenceItems: 2));

        Assert.Equal(2, evidence.Count);
    }

    [Fact]
    public void Retriever_SupportsTurkishQueryNormalization()
    {
        var evidence = Retriever().Retrieve(Project(), Question("delay hangi workflow içinde?"));

        Assert.Contains(evidence, item => item.WorkflowPath == "Main.xaml" && item.ActivityName == "Delay");
    }

    [Fact]
    public void Retriever_RedactsSecretPropertyValues()
    {
        var evidence = Retriever().Retrieve(Project(), Question("password"));

        Assert.Contains(evidence, item => item.PropertyName == "Password" && item.Value == "[REDACTED]");
    }

    [Fact]
    public async Task DirectAnswer_FindsDelayUsageWithoutAi()
    {
        var answer = await Service().AskAsync(Question("Delay nerede kullanılıyor?"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("Delay is used", answer.Answer);
        Assert.Contains("Main.xaml", answer.Answer);
    }

    [Fact]
    public async Task DirectAnswer_FindsGetCredentialUsageWithoutAi()
    {
        var answer = await Service().AskAsync(Question("Where is Get Credential used?"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("GetCredential", answer.Answer);
        Assert.Contains("Framework/Process.xaml", answer.Answer);
    }

    [Fact]
    public async Task DirectAnswer_ExplainsCustomRuleIdWithoutAi()
    {
        var provider = new FakeAssistantProvider { IsConfiguredValue = false };

        var answer = await Service(provider).AskAsync(Question("What does CUSTOM-001 check?", locale: "en"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.False(provider.WasCalled);
        Assert.Contains("CUSTOM-001", answer.Answer);
        Assert.Contains("Workflow", answer.Answer);
    }

    [Fact]
    public async Task DirectAnswer_ReturnsWorkflowCount()
    {
        var answer = await Service().AskAsync(Question("Kaç workflow var?"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("3 workflow", answer.Answer);
    }

    [Fact]
    public async Task DirectAnswer_ReturnsTurkishWhenLocaleIsTurkish()
    {
        var answer = await Service().AskAsync(Question("Kaç workflow var?", locale: "tr"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("Bu projede 3 workflow", answer.Answer);
    }

    [Fact]
    public async Task DirectAnswer_ReturnsEnglishWhenLocaleIsEnglish()
    {
        var answer = await Service().AskAsync(Question("Kaç workflow var?", locale: "en"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("This project has 3 workflow", answer.Answer);
    }

    [Fact]
    public async Task DirectAnswer_ReturnsWorkflowWithMostFindings()
    {
        var answer = await Service().AskAsync(Question("En fazla finding hangi workflow'da?"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("Framework/Process.xaml", answer.Answer);
    }

    [Fact]
    public async Task DirectAnswer_ReturnsOutgoingInvocations()
    {
        var answer = await Service().AskAsync(Question("Main.xaml hangi workflow'ları çağırıyor?"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("Framework/Process.xaml", answer.Answer);
    }

    [Fact]
    public async Task DirectAnswer_ReturnsEnglishOutgoingInvocationsWithoutTreatingWhichAsUncalled()
    {
        var answer = await Service().AskAsync(Question("Which workflows does Main.xaml invoke?", locale: "en"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("Main.xaml calls", answer.Answer);
        Assert.Contains("Framework/Process.xaml", answer.Answer);
        Assert.DoesNotContain("Workflows not called", answer.Answer);
    }

    [Fact]
    public async Task DirectAnswer_ReturnsIncomingInvocations()
    {
        var answer = await Service().AskAsync(Question("Process.xaml kim tarafından çağrılıyor?"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("Main.xaml", answer.Answer);
    }

    [Fact]
    public async Task DirectAnswer_ReturnsExceptionHandlingWorkflowsWithoutAi()
    {
        var answer = await Service().AskAsync(Question("Exception handling sorunu olan workflowları göster."), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("Exception handling findings appear", answer.Answer);
        Assert.Contains("Framework/Process.xaml", answer.Answer);
    }

    [Fact]
    public async Task DirectAnswer_ReturnsMostUsedActivityTypesWithoutAi()
    {
        var answer = await Service().AskAsync(Question("En çok kullanılan activity türleri neler?"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("Most used activity types", answer.Answer);
        Assert.Contains("Delay", answer.Answer);
        Assert.DoesNotContain("Sequence", answer.Answer);
        Assert.DoesNotContain("Variable", answer.Answer);
        Assert.DoesNotContain("VisualBasicValue", answer.Answer);
    }

    [Fact]
    public async Task AnalyticalQuestion_CallsAiProvider()
    {
        var provider = new FakeAssistantProvider { IsConfiguredValue = true };
        var answer = await Service(provider).AskAsync(Question("Maintainability açısından bu proje nasıl?"), CancellationToken.None);

        Assert.True(answer.UsedAi);
        Assert.True(provider.WasCalled);
    }

    [Fact]
    public async Task AnalyticalQuestion_IncludesRetrievedEvidenceInPrompt()
    {
        var provider = new FakeAssistantProvider { IsConfiguredValue = true };
        await Service(provider).AskAsync(Question("Exception handling tasarımı nasıl?"), CancellationToken.None);

        Assert.Contains("RPA003", provider.LastPrompt?.UserContext);
        Assert.Contains("Relevant Evidence", provider.LastPrompt?.UserContext);
    }

    [Fact]
    public async Task AnalyticalQuestion_MapsInsufficientEvidenceAiResponse()
    {
        var provider = new FakeAssistantProvider
        {
            IsConfiguredValue = true,
            Answer = new UiPathProjectAnswer
            {
                Answer = "Evidence is insufficient.",
                AnswerType = UiPathProjectAnswerType.InsufficientEvidence,
                Confidence = UiPathProjectAnswerConfidence.Low,
                UsedAi = true
            }
        };

        var answer = await Service(provider).AskAsync(Question("Bu workflow neden karmaşık görünüyor?"), CancellationToken.None);

        Assert.Equal(UiPathProjectAnswerType.InsufficientEvidence, answer.AnswerType);
        Assert.True(answer.UsedAi);
    }

    [Fact]
    public async Task AnalyticalQuestion_ReturnsUsefulAnswerWhenProviderUnavailable()
    {
        var answer = await Service(new FakeAssistantProvider { IsConfiguredValue = false }).AskAsync(Question("Bu projede en riskli alan ne?"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("AI Review is not configured", answer.Answer);
    }

    [Fact]
    public async Task AnalyticalQuestion_ReturnsTurkishAiNotConfiguredMessage()
    {
        var answer = await Service(new FakeAssistantProvider { IsConfiguredValue = false }).AskAsync(Question("Bu projede en riskli alan ne?", locale: "tr"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("AI İnceleme yapılandırılmamış", answer.Answer);
        Assert.Contains("OPENAI_API_KEY", answer.ErrorMessage);
    }

    [Fact]
    public async Task AnalyticalQuestion_ProviderErrorDoesNotBreakLocalAnalysis()
    {
        var provider = new FakeAssistantProvider { IsConfiguredValue = true, ThrowOnAnswer = true };
        var answer = await Service(provider).AskAsync(Question("Architecture açısından yorumla"), CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("AI-assisted answer could not be completed", answer.Answer);
        Assert.NotEmpty(answer.Evidence);
    }

    private static UiPathProjectRetriever Retriever()
    {
        return new UiPathProjectRetriever(new SensitiveValueRedactor());
    }

    private static UiPathProjectQuestionService Service(FakeAssistantProvider? provider = null, IUiPathAnalysisHistoryService? historyService = null)
    {
        return new UiPathProjectQuestionService(
            new FakeAnalyzer(Project()),
            new UiPathProjectQuestionClassifier(),
            Retriever(),
            new UiPathWorkflowGraphBuilder(),
            new UiPathProjectAssistantPromptBuilder(),
            provider ?? new FakeAssistantProvider(),
            NullLogger<UiPathProjectQuestionService>.Instance,
            customRuleRepository: new InMemoryUiPathCustomRuleRepository([
                new UiPathCustomRuleDefinition
                {
                    Id = "CUSTOM-001",
                    Name = "Large nested workflow",
                    Description = "Detects large nested workflows.",
                    Recommendation = "Split the workflow.",
                    Category = RuleCategory.Maintainability,
                    Severity = RuleSeverity.Warning,
                    Scope = UiPathRuleScope.Workflow,
                    Enabled = true,
                    Weight = 3,
                    MaxPenalty = 12,
                    Conditions =
                    [
                        new UiPathRuleCondition
                        {
                            Field = "Workflow.ExecutableActivityCount",
                            Operator = UiPathRuleConditionOperator.GreaterThanOrEqual,
                            Value = "100"
                        }
                    ]
                }
            ]),
            analysisHistoryService: historyService);
    }

    private static UiPathProjectQuestion Question(string question, int? maxEvidenceItems = null, string? locale = null)
    {
        return new UiPathProjectQuestion
        {
            ProjectPath = "/tmp/project",
            Question = question,
            MaxEvidenceItems = maxEvidenceItems,
            Locale = locale
        };
    }

    private static UiPathProjectAnalysisResult Project()
    {
        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/project",
            ProjectFolderExists = true,
            ProjectJsonExists = true,
            ProjectJsonParsed = true,
            ProjectName = "InvoiceAutomation"
        };

        project.Workflows.Add(Workflow("Main.xaml",
            Activity("Sequence", "Main"),
            Activity("Variable", "counter"),
            Activity("VisualBasicValue", "Value"),
            Activity("Delay", "Delay 5 seconds"),
            Activity("InvokeWorkflowFile", "Invoke Process", ("WorkflowFileName", "Framework\\Process.xaml"))));
        project.Workflows.Add(Workflow("Framework/Process.xaml",
            Activity("Sequence", "Process"),
            Activity("GetCredential", "Get Credential Robot Password", ("Password", "super-secret")),
            Activity("LogMessage", "Log Started")));
        project.Workflows.Add(Workflow("Business/SendMail.xaml",
            Activity("Sequence", "Send Mail"),
            Activity("HTTPRequest", "HTTP Request Send")));
        project.Dependencies.Add(new UiPathDependency { Name = "UiPath.System.Activities", Version = "23.10.0" });
        foreach (var workflow in project.Workflows)
        {
            if (workflow.Analysis is not null)
            {
                workflow.Analysis.Complexity = new UiPathWorkflowMetricsCalculator().CalculateComplexity(workflow.Analysis);
            }
        }

        var analysis = new UiPathStaticAnalysisResult();
        analysis.Findings.Add(Finding("RPA001", "Avoid Delay Activities", RuleSeverity.Warning, RuleCategory.Reliability, "Main.xaml", "Delay activity detected."));
        analysis.Findings.Add(Finding("RPA002", "Empty Catch Block", RuleSeverity.Error, RuleCategory.ExceptionHandling, "Framework/Process.xaml", "Empty Catch block detected."));
        analysis.Findings.Add(Finding("RPA003", "Exception Silently Swallowed", RuleSeverity.Error, RuleCategory.ExceptionHandling, "Framework/Process.xaml", "Exception is silently swallowed."));

        return new UiPathProjectAnalysisResult
        {
            ProjectScan = project,
            Analysis = analysis,
            QualityScore = new UiPathQualityScore
            {
                Score = 82,
                Grade = "B",
                ProfileId = "default",
                ProfileName = "Default",
                TotalFindings = 3,
                ScoreBreakdown =
                [
                    new UiPathRuleScoreBreakdown
                    {
                        RuleId = "RPA003",
                        RuleName = "Exception Silently Swallowed",
                        FindingCount = 1,
                        Severity = RuleSeverity.Error,
                        Weight = 12,
                        RawPenalty = 18,
                        AppliedPenalty = 18,
                        MaxPenalty = 30
                    }
                ]
            },
            Profile = new UiPathRuleProfile { Id = "default", Name = "Default" }
        };
    }

    [Fact]
    public async Task DirectAnswer_ReturnsMostComplexWorkflowWithoutAi()
    {
        var answer = await Service().AskAsync(new UiPathProjectQuestion
        {
            ProjectPath = "/tmp/project",
            Question = "En karmaşık workflow hangisi?",
            Locale = "tr"
        }, CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("En karmaşık workflow", answer.Answer);
    }

    [Fact]
    public async Task DirectAnswer_ReturnsWorkflowComplexityReasonWithoutAi()
    {
        var answer = await Service().AskAsync(new UiPathProjectQuestion
        {
            ProjectPath = "/tmp/project",
            Question = "Process.xaml complexity neden yüksek?",
            Locale = "en"
        }, CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("complexity is", answer.Answer);
        Assert.Contains("Framework/Process.xaml", answer.Answer);
    }

    [Fact]
    public async Task DirectAnswer_ReturnsAnalysisHistoryComparisonWithoutAi()
    {
        using var directory = new TempQuestionHistoryDirectory();
        var history = directory.CreateService();
        history.SaveSnapshot(Project());
        var improved = Project() with
        {
            Analysis = new UiPathStaticAnalysisResult(),
            QualityScore = Project().QualityScore with { Score = 88, Grade = "B", TotalFindings = 0 }
        };
        history.SaveSnapshot(improved);

        var answer = await Service(historyService: history).AskAsync(new UiPathProjectQuestion
        {
            ProjectPath = "/tmp/project",
            Question = "Skor önceki analize göre değişti mi?",
            Locale = "tr"
        }, CancellationToken.None);

        Assert.False(answer.UsedAi);
        Assert.Contains("Önceki analize göre skor", answer.Answer);
    }

    private static UiPathWorkflowInfo Workflow(string path, params UiPathActivityInfo[] activities)
    {
        var analysis = new UiPathWorkflowAnalysis
        {
            FileName = Path.GetFileName(path),
            RelativePath = path
        };
        analysis.Activities.AddRange(activities.Select(activity => activity with { XamlFile = path }));
        return new UiPathWorkflowInfo
        {
            Name = Path.GetFileName(path),
            RelativePath = path,
            FullPath = $"/tmp/project/{path}",
            Analysis = analysis
        };
    }

    private static UiPathActivityInfo Activity(string name, string displayName, params (string Key, string Value)[] properties)
    {
        return new UiPathActivityInfo
        {
            ActivityId = Guid.NewGuid().ToString("N"),
            Name = name,
            DisplayName = displayName,
            TypeName = name,
            XamlFile = "placeholder.xaml",
            Properties = properties.ToDictionary(property => property.Key, property => (string?)property.Value)
        };
    }

    private sealed class TempQuestionHistoryDirectory : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"rpada-question-history-{Guid.NewGuid():N}");

        public IUiPathAnalysisHistoryService CreateService()
        {
            return new UiPathAnalysisHistoryService(new UiPathAnalysisHistoryOptions { StorageRoot = root });
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static UiPathAnalysisFinding Finding(string ruleId, string ruleName, RuleSeverity severity, RuleCategory category, string workflowPath, string message)
    {
        return new UiPathAnalysisFinding
        {
            RuleId = ruleId,
            RuleName = ruleName,
            Severity = severity,
            Category = category,
            Message = message,
            WorkflowPath = workflowPath
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

    private sealed class FakeAssistantProvider : IUiPathProjectAssistantAiProvider
    {
        public bool IsConfiguredValue { get; init; }

        public bool ThrowOnAnswer { get; init; }

        public bool WasCalled { get; private set; }

        public UiPathProjectAssistantPrompt? LastPrompt { get; private set; }

        public UiPathProjectAnswer Answer { get; init; } = new()
        {
            Answer = "AI answer from evidence.",
            AnswerType = UiPathProjectAnswerType.Analytical,
            Confidence = UiPathProjectAnswerConfidence.Medium,
            UsedAi = true,
            RelatedRuleIds = ["RPA003"]
        };

        public string ProviderName => "Fake";

        public bool IsConfigured => IsConfiguredValue;

        public Task<UiPathProjectAnswer> AnswerAsync(UiPathProjectAssistantPrompt prompt, CancellationToken cancellationToken)
        {
            WasCalled = true;
            LastPrompt = prompt;
            if (ThrowOnAnswer)
            {
                throw new InvalidOperationException("Provider failed.");
            }

            return Task.FromResult(Answer);
        }
    }
}

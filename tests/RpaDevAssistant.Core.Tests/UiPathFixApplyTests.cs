using Microsoft.Extensions.Logging.Abstractions;
using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Rules;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Fixes.Providers;
using RpaDevAssistant.Core.Parsing;
using RpaDevAssistant.Core.ProjectAssistant;
using RpaDevAssistant.Core.Scanning;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathFixApplyTests
{
    [Fact]
    public void MutationPolicy_AllowsOnlyLowRiskRpa007DisplayName()
    {
        var policy = new UiPathMutationPolicy();
        var suggestion = Suggestion("RPA007") with { CanAutoApply = true, PropertyName = "DisplayName" };
        var request = Request("/tmp/project", "Main.xaml", suggestion);

        var result = policy.Validate(suggestion, request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("RPA001", "DisplayName", UiPathFixRiskLevel.Low, false, true)]
    [InlineData("RPA002", "DisplayName", UiPathFixRiskLevel.Low, false, true)]
    [InlineData("RPA007", "DisplayName", UiPathFixRiskLevel.Medium, false, true)]
    [InlineData("RPA007", "DisplayName", UiPathFixRiskLevel.High, false, true)]
    [InlineData("RPA007", "DisplayName", UiPathFixRiskLevel.Low, true, true)]
    [InlineData("RPA007", "DisplayName", UiPathFixRiskLevel.Low, false, false)]
    [InlineData("RPA007", "Timeout", UiPathFixRiskLevel.Low, false, true)]
    public void MutationPolicy_RejectsUnsafeFixes(string ruleId, string propertyName, UiPathFixRiskLevel riskLevel, bool requiresAi, bool canAutoApply)
    {
        var policy = new UiPathMutationPolicy();
        var suggestion = Suggestion(ruleId) with
        {
            RiskLevel = riskLevel,
            RequiresAi = requiresAi,
            CanAutoApply = canAutoApply,
            PropertyName = propertyName
        };

        var result = policy.Validate(suggestion, Request("/tmp/project", "Main.xaml", suggestion));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void XamlMutation_ChangesDisplayNameAndKeepsOtherAttributes()
    {
        using var project = TestProject.Create();
        var workflow = project.WriteWorkflow("Business/Login.xaml", ClickWorkflow("Click_1", "Click", "btnLogin", extraAttribute: "ContinueOnError=\"False\""));

        var result = new UiPathXamlMutationService().BuildMutation(new UiPathXamlMutationRequest
        {
            ProjectPath = project.RootPath,
            WorkflowPath = "Business/Login.xaml",
            WorkflowFullPath = workflow,
            Locator = new UiPathActivityLocator { WorkflowPath = "Business/Login.xaml", IdRef = "Click_1", XamlElementName = "Click" },
            PropertyName = "DisplayName",
            ExpectedCurrentValue = "Click",
            SuggestedValue = "Click Login"
        });

        Assert.True(result.Success, result.Message);
        Assert.Contains("DisplayName=\"Click Login\"", result.MutatedContent);
        Assert.Contains("ContinueOnError=\"False\"", result.MutatedContent);
        Assert.Contains("xmlns:ui=\"http://schemas.uipath.com/workflow/activities\"", result.MutatedContent);
    }

    [Fact]
    public void XamlMutation_FindsNestedActivityByStableId()
    {
        using var project = TestProject.Create();
        var workflow = project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence DisplayName="Main">
            <Sequence.Activities>
              <Sequence DisplayName="Inner">
                <Sequence.Activities>
                  <ui:Click DisplayName="Click" sap2010:WorkflowViewState.IdRef="Click_Nested" Target="btnLogin" />
                </Sequence.Activities>
              </Sequence>
            </Sequence.Activities>
          </Sequence>
        """));

        var result = new UiPathXamlMutationService().BuildMutation(new UiPathXamlMutationRequest
        {
            ProjectPath = project.RootPath,
            WorkflowPath = "Main.xaml",
            WorkflowFullPath = workflow,
            Locator = new UiPathActivityLocator { WorkflowPath = "Main.xaml", IdRef = "Click_Nested", XamlElementName = "Click" },
            PropertyName = "DisplayName",
            ExpectedCurrentValue = "Click",
            SuggestedValue = "Click Login"
        });

        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public void XamlMutation_RejectsAmbiguousFallbackLocator()
    {
        using var project = TestProject.Create();
        var workflow = project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence DisplayName="Main">
            <Sequence.Activities>
              <ui:Click DisplayName="Click" Target="btnLogin" />
              <ui:Click DisplayName="Click" Target="btnCancel" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = new UiPathXamlMutationService().BuildMutation(new UiPathXamlMutationRequest
        {
            ProjectPath = project.RootPath,
            WorkflowPath = "Main.xaml",
            WorkflowFullPath = workflow,
            Locator = new UiPathActivityLocator { WorkflowPath = "Main.xaml", XamlElementName = "Click" },
            PropertyName = "DisplayName",
            ExpectedCurrentValue = "Click",
            SuggestedValue = "Click Login"
        });

        Assert.False(result.Success);
        Assert.Equal("ambiguous_activity", result.ErrorCode);
    }

    [Fact]
    public void XamlMutation_RejectsExpectedCurrentValueMismatch()
    {
        using var project = TestProject.Create();
        var workflow = project.WriteWorkflow("Main.xaml", ClickWorkflow("Click_1", "Click Submit", "btnLogin"));

        var result = new UiPathXamlMutationService().BuildMutation(new UiPathXamlMutationRequest
        {
            ProjectPath = project.RootPath,
            WorkflowPath = "Main.xaml",
            WorkflowFullPath = workflow,
            Locator = new UiPathActivityLocator { WorkflowPath = "Main.xaml", IdRef = "Click_1", XamlElementName = "Click" },
            PropertyName = "DisplayName",
            ExpectedCurrentValue = "Click",
            SuggestedValue = "Click Login"
        });

        Assert.False(result.Success);
        Assert.Equal("stale_current_value", result.ErrorCode);
    }

    [Fact]
    public async Task ApplyFix_AppliesDisplayNameCreatesBackupAndAuditLog()
    {
        using var project = TestProject.Create();
        var workflow = project.WriteWorkflow("Business/Login.xaml", ClickWorkflow("Click_Login", "Click", "btnLogin"));
        var services = Services();
        var suggestion = await services.Suggestions.SuggestAsync(new UiPathFixSuggestionRequest
        {
            ProjectPath = project.RootPath,
            RuleId = "RPA007",
            WorkflowPath = "Business/Login.xaml",
            ActivityId = "Click_Login",
            PropertyName = "DisplayName"
        }, CancellationToken.None);

        var result = await services.Applier.ApplyAsync(Request(project.RootPath, "Business/Login.xaml", suggestion.Suggestion!), CancellationToken.None);

        Assert.True(result.Success, $"{result.Message} {string.Join("; ", result.ValidationResult.Errors)}");
        Assert.True(result.Applied);
        Assert.True(result.RequiresReanalysis);
        Assert.Contains("DisplayName=\"Click Login\"", File.ReadAllText(workflow));
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));
        Assert.True(File.Exists(Path.Combine(project.RootPath, ".rpadevassistant", "backups", result.BackupId!, "backup.json")));
        Assert.True(File.Exists(Path.Combine(project.RootPath, ".rpadevassistant", "logs", "mutations.jsonl")));
    }

    [Fact]
    public async Task ApplyFix_RejectsPathTraversal()
    {
        using var project = TestProject.Create();
        var services = Services();

        var result = await services.Applier.ApplyAsync(new UiPathFixApplyRequest
        {
            ProjectPath = project.RootPath,
            FixSuggestionId = "fix",
            RuleId = "RPA007",
            WorkflowPath = "../outside.xaml",
            PropertyName = "DisplayName",
            ExpectedCurrentValue = "Click",
            SuggestedValue = "Click Login"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("path_traversal", result.ErrorCode);
    }

    [Fact]
    public async Task ApplyFix_RejectsStaleFileHash()
    {
        using var project = TestProject.Create();
        project.WriteWorkflow("Main.xaml", ClickWorkflow("Click_1", "Click", "btnLogin"));
        var services = Services();
        var suggestion = await services.Suggestions.SuggestAsync(new UiPathFixSuggestionRequest
        {
            ProjectPath = project.RootPath,
            RuleId = "RPA007",
            WorkflowPath = "Main.xaml",
            ActivityId = "Click_1",
            PropertyName = "DisplayName"
        }, CancellationToken.None);

        var request = Request(project.RootPath, "Main.xaml", suggestion.Suggestion!) with { ExpectedFileHash = "not-the-current-hash" };
        var result = await services.Applier.ApplyAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("stale_file_hash", result.ErrorCode);
    }

    [Fact]
    public async Task ApplyFix_RollsBackWhenPostValidationFails()
    {
        using var project = TestProject.Create();
        var workflow = project.WriteWorkflow("Main.xaml", ClickWorkflow("Click_1", "Click", "btnLogin"));
        var services = Services(new InvalidPostWriteMutationService());
        var suggestion = await services.Suggestions.SuggestAsync(new UiPathFixSuggestionRequest
        {
            ProjectPath = project.RootPath,
            RuleId = "RPA007",
            WorkflowPath = "Main.xaml",
            ActivityId = "Click_1",
            PropertyName = "DisplayName"
        }, CancellationToken.None);

        var result = await services.Applier.ApplyAsync(Request(project.RootPath, "Main.xaml", suggestion.Suggestion!), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("post_validation_failed", result.ErrorCode);
        Assert.Contains("DisplayName=\"Click\"", File.ReadAllText(workflow));
    }

    [Fact]
    public void Scanner_IgnoresAssistantBackupFolder()
    {
        using var project = TestProject.Create();
        project.WriteWorkflow("Main.xaml", ClickWorkflow("Click_1", "Click Login", "btnLogin"));
        project.WriteWorkflow(".rpadevassistant/backups/20260830-021530123/Main.xaml", ClickWorkflow("Click_2", "Click", "btnLogin"));

        var scan = new UiPathProjectScanner().Scan(project.RootPath);

        Assert.Single(scan.Workflows);
        Assert.Equal("Main.xaml", scan.Workflows.Single().RelativePath);
    }

    [Fact]
    public async Task ApplyFix_EndToEnd_RemovesRpa007FindingAfterReanalysis()
    {
        using var project = TestProject.Create();
        project.WriteWorkflow("Main.xaml", ClickWorkflow("Click_1", "Click", "btnLogin"));
        var services = Services();
        var before = services.Analyzer.Analyze(project.RootPath);
        var finding = before.Analysis.Findings.Single(item => item.RuleId == "RPA007");
        var suggestion = await services.Suggestions.SuggestAsync(new UiPathFixSuggestionRequest
        {
            ProjectPath = project.RootPath,
            RuleId = "RPA007",
            WorkflowPath = finding.WorkflowPath,
            ActivityId = finding.ActivityId,
            PropertyName = finding.PropertyName
        }, CancellationToken.None);

        var apply = await services.Applier.ApplyAsync(Request(project.RootPath, "Main.xaml", suggestion.Suggestion!), CancellationToken.None);
        var after = services.Analyzer.Analyze(project.RootPath);

        Assert.True(apply.Success, $"{apply.Message} {string.Join("; ", apply.ValidationResult.Errors)}");
        Assert.DoesNotContain(after.Analysis.Findings, item => item.RuleId == "RPA007");
    }

    private static ServiceBundle Services(IUiPathXamlMutationService? mutationService = null)
    {
        var parser = new UiPathXamlParser();
        var scanner = new UiPathProjectScanner(parser);
        var rules = new IUiPathAnalysisRule[] { new GenericActivityDisplayNameRule() };
        var analyzer = new UiPathProjectAnalyzer(
            scanner,
            new UiPathRuleEngine(rules),
            new BuiltInUiPathRuleProfileProvider(),
            new UiPathQualityScoringEngine());
        var suggestions = new UiPathFixSuggestionService(
            analyzer,
            new UiPathFixSuggestionRegistry([new DisplayNameFixSuggestionProvider()]),
            new UiPathFixContextBuilder(new UiPathWorkflowGraphBuilder()),
            new UiPathFixSuggestionValidator(),
            new UiPathAiFixPromptBuilder(new SensitiveValueRedactor()),
            new FakeAiFixAdvisor(),
            NullLogger<UiPathFixSuggestionService>.Instance);
        var applier = new UiPathFixApplier(
            suggestions,
            analyzer,
            new UiPathMutationPolicy(),
            mutationService ?? new UiPathXamlMutationService(),
            new UiPathBackupService(),
            new UiPathMutationAuditLogger(),
            parser,
            scanner,
            new UiPathMutationLock());
        return new ServiceBundle(analyzer, suggestions, applier);
    }

    private static UiPathFixApplyRequest Request(string projectPath, string workflowPath, UiPathFixSuggestion suggestion)
    {
        return new UiPathFixApplyRequest
        {
            ProjectPath = projectPath,
            FixSuggestionId = suggestion.Id,
            RuleId = suggestion.RuleId,
            WorkflowPath = workflowPath,
            ActivityId = suggestion.ActivityId,
            PropertyName = suggestion.PropertyName ?? "DisplayName",
            ExpectedCurrentValue = suggestion.CurrentValue,
            SuggestedValue = suggestion.SuggestedValue ?? string.Empty,
            ExpectedFileHash = suggestion.ExpectedFileHash,
            CreateBackup = true
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
            FixType = UiPathFixSuggestionType.NamingChange,
            Confidence = UiPathFixConfidence.High,
            RiskLevel = UiPathFixRiskLevel.Low,
            PropertyName = "DisplayName",
            CurrentValue = "Click",
            SuggestedValue = "Click Login"
        };
    }

    private static string ClickWorkflow(string idRef, string displayName, string target, string extraAttribute = "")
    {
        return Workflow($"""
          <Sequence DisplayName="Main">
            <Sequence.Activities>
              <ui:Click DisplayName="{displayName}" sap2010:WorkflowViewState.IdRef="{idRef}" Target="{target}" {extraAttribute} />
            </Sequence.Activities>
          </Sequence>
        """);
    }

    private static string Workflow(string body)
    {
        return $$"""
        <Activity
          xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:ui="http://schemas.uipath.com/workflow/activities"
          xmlns:sap2010="http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation"
          x:Class="Main">
        {{body}}
        </Activity>
        """;
    }

    private sealed record ServiceBundle(IUiPathProjectAnalyzer Analyzer, IUiPathFixSuggestionService Suggestions, IUiPathFixApplier Applier);

    private sealed class FakeAiFixAdvisor : IUiPathAiFixAdvisor
    {
        public string ProviderName => "Fake";

        public bool IsConfigured => false;

        public Task<UiPathFixSuggestion> SuggestAsync(UiPathAiFixPrompt prompt, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class InvalidPostWriteMutationService : IUiPathXamlMutationService
    {
        public UiPathXamlMutationResult BuildMutation(UiPathXamlMutationRequest request)
        {
            var content = File.ReadAllText(request.WorkflowFullPath).Replace("DisplayName=\"Click\"", "DisplayName=\"Wrong\"", StringComparison.Ordinal);
            return new UiPathXamlMutationResult
            {
                Success = true,
                Message = "Invalid mutation prepared.",
                PreviousValue = request.ExpectedCurrentValue,
                NewValue = request.SuggestedValue,
                MutatedContent = content
            };
        }
    }

    private sealed class TestProject : IDisposable
    {
        public string RootPath { get; } = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantApplyTests-{Guid.NewGuid():N}");

        private TestProject()
        {
            Directory.CreateDirectory(RootPath);
            File.WriteAllText(Path.Combine(RootPath, "project.json"), """
            {
              "name": "ApplyProject",
              "targetFramework": "Windows",
              "dependencies": {}
            }
            """);
        }

        public static TestProject Create() => new();

        public string WriteWorkflow(string relativePath, string content)
        {
            var fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}

using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.ProcessUnderstanding;
using RpaDevAssistant.Core.ProjectAssistant;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class ProcessPddAnalysisServiceTests
{
    [Fact]
    public void Analyze_ExtractsGroundedSystemsAndBusinessRules_AndExcludesTechnicalRetry()
    {
        var analysis = CreateAnalysis(
            Activity("browser", "UseApplicationBrowser", "Open Rota", new() { ["Url"] = "https://rota.company.com/customer?id=secret" }),
            Activity("business", "If", "Outstanding debt eligibility", new() { ["Condition"] = "BorcTutari > 0" }),
            Activity("technical", "If", "Retry browser", new() { ["Condition"] = "retryCount < 3 AndAlso browserExists" }));

        var result = CreateService().Analyze(analysis, new ProcessPddDocument
        {
            FileName = "Process.md",
            Content = "# Business Rules\n- Only records with an outstanding debt amount are processed."
        });

        var system = Assert.Single(result.Systems, item => item.Name == "rota.company.com");
        Assert.Equal("Web Application", system.Type);
        Assert.DoesNotContain("secret", system.Evidence, StringComparison.OrdinalIgnoreCase);
        var rule = Assert.Single(result.ProjectBusinessRules);
        Assert.Equal("Main.xaml", rule.WorkflowPath);
        Assert.Contains("BorcTutari", rule.Condition);
        Assert.DoesNotContain(result.ProjectBusinessRules, item => item.Title.Contains("Retry", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(BusinessRuleDocumentationStatus.Documented, Assert.Single(result.GapAnalysis).Status);
    }

    [Fact]
    public void Analyze_ExtractsPddSourceReference_AndLabelsUnmatchedRuleAsPossibleGap()
    {
        var analysis = CreateAnalysis(Activity("business", "FlowDecision", "Reject invalid identity", new() { ["Condition"] = "String.IsNullOrWhiteSpace(TCKN)" }));

        var result = CreateService().Analyze(analysis, new ProcessPddDocument
        {
            FileName = "PDD.txt",
            Content = "Business Rules:\n1. Records with debt are processed."
        }, "tr");

        Assert.Contains("line 2", Assert.Single(result.PddBusinessRules).SourceReference);
        var gap = Assert.Single(result.GapAnalysis);
        Assert.Equal(BusinessRuleDocumentationStatus.PossiblyMissing, gap.Status);
        Assert.NotNull(gap.SuggestedPddAddition);
        Assert.Contains("yeterince benzer", gap.Reason);
    }

    [Fact]
    public void Analyze_RedactsSensitiveBusinessRuleConditionAndEvidence()
    {
        var analysis = CreateAnalysis(Activity("business", "If", "Customer token eligibility", new()
        {
            ["Condition"] = "customerToken = \"FakeSecret123!\""
        }));

        var result = CreateService().Analyze(analysis, new ProcessPddDocument
        {
            FileName = "PDD.md",
            Content = "# Business Rules\n- Customer records must have a valid token."
        });

        var rule = Assert.Single(result.ProjectBusinessRules);
        Assert.Equal("[REDACTED]", rule.Condition);
        Assert.DoesNotContain("FakeSecret123!", rule.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_ExtractsBusinessRuleExceptionWithHighConfidence()
    {
        var analysis = CreateAnalysis(Activity("throw", "Throw", "Reject invalid record", new()
        {
            ["Exception"] = "New BusinessRuleException(\"Invalid customer record\")"
        }));

        var rule = Assert.Single(CreateService().Analyze(analysis, EmptyPdd()).ProjectBusinessRules);

        Assert.Equal(UiPathFixConfidence.High, rule.Confidence);
        Assert.Equal("BusinessRuleException", rule.Outcome);
        Assert.Equal("Throw", rule.Activity);
    }

    [Fact]
    public void Analyze_ExtractsSwitchConditionAndSerializedCases()
    {
        var analysis = CreateAnalysis(Activity("switch", "Switch", "Route transaction status", new()
        {
            ["Expression"] = "TransactionStatus",
            ["Cases"] = "Approved, Rejected, Skipped"
        }));

        var rule = Assert.Single(CreateService().Analyze(analysis, EmptyPdd()).ProjectBusinessRules);

        Assert.Equal("TransactionStatus", rule.Condition);
        Assert.Equal("Approved, Rejected, Skipped", rule.Outcome);
    }

    [Fact]
    public void Analyze_ExcludesPurelyTechnicalControlFlow()
    {
        var analysis = CreateAnalysis(
            Activity("retry", "If", "Retry browser", new() { ["Condition"] = "retryCount < 3" }),
            Activity("file", "If", "Check file exists", new() { ["Condition"] = "File.Exists(filePath)" }),
            Activity("selector", "FlowDecision", "Selector ready", new() { ["Condition"] = "selector IsNot Nothing" }),
            Activity("timeout", "If", "Timeout control", new() { ["Condition"] = "elapsedMs > timeoutMs" }));

        Assert.Empty(CreateService().Analyze(analysis, EmptyPdd()).ProjectBusinessRules);
    }

    [Fact]
    public void Analyze_KeepsConditionWhenTechnicalAndBusinessEvidenceCoexist()
    {
        var analysis = CreateAnalysis(Activity("mixed", "If", "Retry eligible debt record", new()
        {
            ["Condition"] = "retryCount < 3 AndAlso BorcTutari > 0"
        }));

        var rule = Assert.Single(CreateService().Analyze(analysis, EmptyPdd()).ProjectBusinessRules);

        Assert.Equal(UiPathFixConfidence.Medium, rule.Confidence);
        Assert.Contains("BorcTutari", rule.Condition);
    }

    [Fact]
    public void Analyze_LowConfidenceReviewableConditionBecomesNeedsReview()
    {
        var analysis = CreateAnalysis(Activity("review", "If", "Validation checkpoint", new()
        {
            ["Condition"] = "resultCode = 1"
        }));

        var result = CreateService().Analyze(analysis, EmptyPdd());

        Assert.Equal(UiPathFixConfidence.Low, Assert.Single(result.ProjectBusinessRules).Confidence);
        Assert.Equal(BusinessRuleDocumentationStatus.NeedsReview, Assert.Single(result.GapAnalysis).Status);
        Assert.Null(result.GapAnalysis[0].SuggestedPddAddition);
    }

    [Fact]
    public void Analyze_ExtractsRuleEmbeddedInNormalPddProse()
    {
        var analysis = CreateAnalysis(Activity("business", "If", "Reject invalid record", new() { ["Condition"] = "RecordStatus = \"Invalid\"" }));

        var result = CreateService().Analyze(analysis, new ProcessPddDocument
        {
            FileName = "PDD.txt",
            Content = "Process description\nInvalid records are rejected before queue submission."
        });

        var pddRule = Assert.Single(result.PddBusinessRules);
        Assert.Contains("Invalid records", pddRule.SourceSnippet);
        Assert.Contains("line 2", pddRule.SourceReference);
    }

    [Fact]
    public void Analyze_DoesNotTreatEveryParagraphUnderRuleHeadingAsARule()
    {
        var result = CreateService().Analyze(CreateAnalysis(), new ProcessPddDocument
        {
            FileName = "PDD.md",
            Content = "# Business Rules\nThis section provides background information about the process."
        });

        Assert.Empty(result.PddBusinessRules);
    }

    [Fact]
    public void Analyze_RetainsMatchedPddEvidence()
    {
        var analysis = CreateAnalysis(Activity("business", "If", "Outstanding debt eligibility", new() { ["Condition"] = "BorcTutari > 0" }));

        var comparison = Assert.Single(CreateService().Analyze(analysis, new ProcessPddDocument
        {
            FileName = "PDD.md",
            Content = "# Business Rules\n- Only records with an outstanding debt amount are processed."
        }).GapAnalysis);

        Assert.Equal(BusinessRuleDocumentationStatus.Documented, comparison.Status);
        Assert.NotNull(comparison.MatchedPddRule);
        Assert.Equal(comparison.MatchedPddRuleId, comparison.MatchedPddRule!.Id);
        Assert.Contains("line 2", comparison.MatchedPddRule.SourceReference);
    }

    [Fact]
    public void Analyze_BuildsFlowFromStaticInvocationGraphAndReportsReFramework()
    {
        var main = Workflow("Main.xaml", Activity("invoke", "InvokeWorkflowFile", "Invoke Process", new() { ["WorkflowFileName"] = "Framework/Process.xaml" }));
        var process = Workflow("Framework/Process.xaml", Activity("log", "LogMessage", "Started", new()));
        var analysis = CreateAnalysis([main, process], isReFramework: true);

        var result = CreateService().Analyze(analysis, EmptyPdd(), "en");

        Assert.Equal(["Main.xaml", "Framework/Process.xaml"], result.ProcessFlow.Select(step => step.WorkflowPath));
        Assert.Contains("REFramework", result.ProcessSummary);
        Assert.DoesNotContain(result.Systems, system => system.Type == "File System");
    }

    [Fact]
    public void Analyze_DoesNotInferBranchOrderAndReportsTruncatedFlow()
    {
        var workflows = new List<UiPathWorkflowInfo>();
        for (var index = 0; index < 14; index++)
        {
            var path = index == 0 ? "Main.xaml" : $"Step{index:D2}.xaml";
            var activities = index < 13
                ? new[] { Activity($"invoke-{index}", "InvokeWorkflowFile", $"Invoke step {index + 1}", new() { ["WorkflowFileName"] = $"Step{index + 1:D2}.xaml" }) }
                : [];
            workflows.Add(Workflow(path, activities));
        }
        var mainAnalysis = workflows[0].Analysis!;
        mainAnalysis.Activities.Add(Activity("branch", "InvokeWorkflowFile", "Invoke branch", new() { ["WorkflowFileName"] = "Branch.xaml" }));
        workflows.Add(Workflow("Branch.xaml", Activity("log", "LogMessage", "Branch", new())));

        var result = CreateService().Analyze(CreateAnalysis(workflows), EmptyPdd());

        Assert.Equal(12, result.ProcessFlow.Count);
        Assert.Equal(3, result.OmittedProcessFlowCount);
        Assert.All(result.ProcessFlow.Skip(1), step => Assert.Contains("Static Invoke Workflow File", step.Evidence));
    }

    [Fact]
    public void Analyze_ProducesValidSummaryWhenProjectHasNoWorkflowGraph()
    {
        var result = CreateService().Analyze(CreateAnalysis(Array.Empty<UiPathWorkflowInfo>()), EmptyPdd(), "en");

        Assert.Empty(result.ProcessFlow);
        Assert.Equal(0, result.OmittedProcessFlowCount);
        Assert.Contains("No static invocation order was established", result.ProcessSummary);
    }

    [Fact]
    public void Analyze_ExtractsApplicationDatabaseAndMultipleSystemEvidence()
    {
        var analysis = CreateAnalysis(
            Activity("app-1", "UseApplication", "Open ERP", new()),
            Activity("app-2", "StartProcess", "Start ERP client", new()),
            Activity("db", "ExecuteSql", "Read customer database", new()));

        var systems = CreateService().Analyze(analysis, EmptyPdd()).Systems;

        var desktop = Assert.Single(systems, system => system.Name == "Desktop Application");
        Assert.Contains("Open ERP", desktop.Evidence);
        Assert.Contains("Start ERP client", desktop.Evidence);
        Assert.Contains(systems, system => system.Type == "Database");
    }

    [Fact]
    public void Analyze_MissingSuggestionIsBusinessOrientedAndLocalized()
    {
        var analysis = CreateAnalysis(Activity("business", "If", "Reject invalid identity", new() { ["Condition"] = "String.IsNullOrWhiteSpace(TCKN)" }));

        var tr = Assert.Single(CreateService().Analyze(analysis, EmptyPdd(), "tr").GapAnalysis);
        var en = Assert.Single(CreateService().Analyze(analysis, EmptyPdd(), "en").GapAnalysis);

        Assert.Contains("PDD içinde tanımlanmalıdır", tr.SuggestedPddAddition);
        Assert.Contains("should be documented in the PDD", en.SuggestedPddAddition);
        Assert.DoesNotContain("String.IsNullOrWhiteSpace", tr.SuggestedPddAddition);
    }

    [Fact]
    public void Analyze_DeduplicatesStableBusinessRuleEvidence()
    {
        var activity = Activity("same-id", "If", "Outstanding debt eligibility", new() { ["Condition"] = "BorcTutari > 0" });

        var result = CreateService().Analyze(CreateAnalysis(activity, activity), EmptyPdd());

        Assert.Single(result.ProjectBusinessRules);
    }

    private static ProcessPddAnalysisService CreateService() => new(new UiPathWorkflowGraphBuilder(), new SensitiveValueRedactor());

    private static UiPathProjectAnalysisResult CreateAnalysis(params UiPathActivityInfo[] activities)
    {
        return CreateAnalysis([Workflow("Main.xaml", activities)]);
    }

    private static UiPathProjectAnalysisResult CreateAnalysis(IReadOnlyList<UiPathWorkflowInfo> workflows, bool isReFramework = false)
    {
        var scan = new ProjectScanResult { ProjectPath = "/tmp/process-project", ProjectName = "Process Project", IsReFramework = isReFramework };
        scan.Workflows.AddRange(workflows);
        return new UiPathProjectAnalysisResult
        {
            ProjectScan = scan,
            Analysis = new UiPathStaticAnalysisResult(),
            Profile = new UiPathRuleProfile { Id = "default", Name = "Default" },
            QualityScore = new UiPathQualityScore { Score = 100, Grade = "A", ProfileId = "default", ProfileName = "Default" }
        };
    }

    private static UiPathWorkflowInfo Workflow(string relativePath, params UiPathActivityInfo[] activities)
    {
        var workflowAnalysis = new UiPathWorkflowAnalysis { FileName = Path.GetFileName(relativePath), RelativePath = relativePath };
        workflowAnalysis.Activities.AddRange(activities);
        return new UiPathWorkflowInfo { Name = Path.GetFileName(relativePath), RelativePath = relativePath, FullPath = Path.Combine("/tmp/process-project", relativePath), Analysis = workflowAnalysis };
    }

    private static ProcessPddDocument EmptyPdd() => new() { FileName = "PDD.md", Content = "# Overview\nNo rules listed." };

    private static UiPathActivityInfo Activity(string id, string name, string displayName, Dictionary<string, string?> properties) => new()
    {
        ActivityId = id,
        Name = name,
        DisplayName = displayName,
        TypeName = name,
        XamlFile = "Main.xaml",
        Properties = properties
    };
}

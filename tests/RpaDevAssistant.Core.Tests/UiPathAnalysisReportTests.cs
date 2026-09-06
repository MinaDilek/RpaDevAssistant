using System.Text.Json;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Reporting;
using RpaDevAssistant.Core.Reporting.Export;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathAnalysisReportTests
{
    [Fact]
    public void Build_CreatesSummary()
    {
        var report = BuildReport(Findings(Finding("RPA001", RuleSeverity.Warning, "Main.xaml"), Finding("RPA002", RuleSeverity.Error, "Business.xaml")));

        Assert.Equal(2, report.Summary.TotalFindings);
        Assert.Equal(1, report.Summary.ErrorCount);
        Assert.Equal(1, report.Summary.WarningCount);
        Assert.Equal(2, report.Summary.WorkflowsWithFindings);
    }

    [Fact]
    public void Build_OrdersFindingsBySeverityWorkflowRuleAndActivity()
    {
        var report = BuildReport(Findings(
            Finding("RPA008", RuleSeverity.Suggestion, "B.xaml", "Z"),
            Finding("RPA001", RuleSeverity.Warning, "A.xaml", "Delay"),
            Finding("RPA005", RuleSeverity.Error, "C.xaml", "Invoke"),
            Finding("RPA002", RuleSeverity.Critical, "D.xaml", "Catch")));

        Assert.Equal(["RPA002", "RPA005", "RPA001", "RPA008"], report.Findings.Select(finding => finding.RuleId));
    }

    [Fact]
    public void Build_CreatesWorkflowSummaries()
    {
        var report = BuildReport(Findings(Finding("RPA002", RuleSeverity.Error, "Main.xaml")));

        var main = report.WorkflowSummaries.Single(workflow => workflow.RelativePath == "Main.xaml");
        Assert.Equal(1, main.FindingCount);
        Assert.Equal(1, main.ErrorCount);
        Assert.Equal("High Risk", main.Status);
    }

    [Fact]
    public void Build_CalculatesCleanWorkflowCount()
    {
        var report = BuildReport(Findings(Finding("RPA001", RuleSeverity.Warning, "Main.xaml")));

        Assert.Equal(1, report.Summary.WorkflowsWithFindings);
        Assert.Equal(1, report.Summary.CleanWorkflows);
    }

    [Fact]
    public void Build_CalculatesTopRules()
    {
        var report = BuildReport(Findings(
            Finding("RPA001", RuleSeverity.Warning, "Main.xaml"),
            Finding("RPA001", RuleSeverity.Warning, "Main.xaml"),
            Finding("RPA002", RuleSeverity.Error, "Business.xaml")));

        Assert.Equal("RPA001 Test Rule", report.Summary.TopRules.First().Name);
        Assert.Equal(2, report.Summary.TopRules.First().Count);
    }

    [Fact]
    public void JsonExporter_ProducesValidJson()
    {
        var export = new JsonUiPathReportExporter().Export(BuildReport(Findings()));

        using var document = JsonDocument.Parse(export.Content);
        Assert.Equal("RPA Dev Assistant", document.RootElement.GetProperty("productName").GetString());
    }

    [Fact]
    public void JsonExporter_IncludesSchemaVersion()
    {
        var export = new JsonUiPathReportExporter().Export(BuildReport(Findings()));

        Assert.Contains("\"schemaVersion\": \"1.0\"", export.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlExporter_ProducesStandaloneDocument()
    {
        var export = new HtmlUiPathReportExporter().Export(BuildReport(Findings()));

        Assert.StartsWith("<!doctype html>", export.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<style>", export.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UiPath Project Analysis Report", export.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlExporter_EncodesSpecialCharacters()
    {
        var report = BuildReport(Findings(Finding("RPA001", RuleSeverity.Warning, "Main<script>.xaml", "<Click>")));

        var export = new HtmlUiPathReportExporter().Export(report);

        Assert.Contains("&lt;Click&gt;", export.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("<Click>", export.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlExporter_DoesNotRenderNullOptionalFields()
    {
        var export = new HtmlUiPathReportExporter().Export(BuildReport(Findings(new UiPathAnalysisFinding
        {
            RuleId = "RPA001",
            RuleName = "Test Rule",
            Severity = RuleSeverity.Warning,
            Category = RuleCategory.Reliability,
            Message = "Message"
        })));

        Assert.DoesNotContain("Current Value", export.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Recommendation</div>", export.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlExporter_LocalizesTurkishFindingText()
    {
        var export = new HtmlUiPathReportExporter().Export(BuildReport(Findings(Finding("RPA003", RuleSeverity.Error, "Main.xaml"))), "tr");

        Assert.Contains("<html lang=\"tr\">", export.Content, StringComparison.Ordinal);
        Assert.Contains("UiPath Proje Analiz Raporu", export.Content, StringComparison.Ordinal);
        Assert.Contains("Oluşturulma Tarihi", export.Content, StringComparison.Ordinal);
        Assert.Contains("Proje &#214;zeti", export.Content, StringComparison.Ordinal);
        Assert.Contains("Skor", export.Content, StringComparison.Ordinal);
        Assert.Contains("&#214;neri", export.Content, StringComparison.Ordinal);
        Assert.Contains("Exception sessizce yutuluyor", export.Content, StringComparison.Ordinal);
        Assert.Contains("Catch bloğunda", export.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlExporter_LocalizesEnglishFindingText()
    {
        var export = new HtmlUiPathReportExporter().Export(BuildReport(Findings(Finding("RPA003", RuleSeverity.Error, "Main.xaml"))), "en");

        Assert.Contains("<html lang=\"en\">", export.Content, StringComparison.Ordinal);
        Assert.Contains("UiPath Project Analysis Report", export.Content, StringComparison.Ordinal);
        Assert.Contains("Generated At", export.Content, StringComparison.Ordinal);
        Assert.Contains("Project Summary", export.Content, StringComparison.Ordinal);
        Assert.Contains("Score", export.Content, StringComparison.Ordinal);
        Assert.Contains("Recommendation", export.Content, StringComparison.Ordinal);
        Assert.Contains("Exception Silently Swallowed", export.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void FileNameGenerator_RemovesInvalidWindowsCharacters()
    {
        var fileName = UiPathReportFileNameGenerator.Generate("Invoice:Bot*?", new DateTimeOffset(2026, 8, 29, 22, 45, 0, TimeSpan.Zero), UiPathReportExportFormat.Html);

        Assert.Equal("Invoice-Bot-RPA-Analysis-20260829-224500.html", fileName);
    }

    [Fact]
    public void FileNameGenerator_UsesPdfExtension()
    {
        var fileName = UiPathReportFileNameGenerator.Generate("Invoice Bot", new DateTimeOffset(2026, 8, 29, 22, 45, 0, TimeSpan.Zero), UiPathReportExportFormat.Pdf);

        Assert.Equal("Invoice-Bot-RPA-Analysis-20260829-224500.pdf", fileName);
    }

    [Fact]
    public void Exporters_ReturnExpectedContentTypes()
    {
        var report = BuildReport(Findings());

        Assert.Equal("application/json; charset=utf-8", new JsonUiPathReportExporter().Export(report).ContentType);
        Assert.Equal("text/html; charset=utf-8", new HtmlUiPathReportExporter().Export(report).ContentType);
        Assert.Equal("application/pdf", new PdfUiPathReportExporter().Export(report).ContentType);
    }

    [Fact]
    public void PdfExporter_ProducesValidPdfHeader()
    {
        var export = new PdfUiPathReportExporter().Export(BuildReport(Findings(Finding("RPA001", RuleSeverity.Warning, "Main.xaml"))));

        Assert.StartsWith("%PDF-1.4", export.Content, StringComparison.Ordinal);
        Assert.Contains("RPA Dev Assistant Analysis Report", export.Content, StringComparison.Ordinal);
        Assert.Contains("%%EOF", export.Content, StringComparison.Ordinal);
    }

    private static UiPathAnalysisReport BuildReport(UiPathStaticAnalysisResult analysis)
    {
        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/Project",
            ProjectName = "Invoice<Bot>",
            Compatibility = "Windows",
            ProjectFolderExists = true,
            ProjectJsonExists = true,
            ProjectJsonParsed = true
        };

        project.Workflows.Add(Workflow("Main.xaml", 3));
        project.Workflows.Add(Workflow("Business.xaml", 2));

        var profile = new UiPathRuleProfile
        {
            Id = "default",
            Name = "Default"
        };
        var score = new UiPathQualityScore
        {
            Score = 84,
            Grade = "B",
            ProfileId = "default",
            ProfileName = "Default",
            TotalFindings = analysis.TotalFindings,
            ScoreBreakdown =
            [
                new UiPathRuleScoreBreakdown
                {
                    RuleId = "RPA001",
                    RuleName = "Test Rule",
                    FindingCount = 1,
                    Severity = RuleSeverity.Warning,
                    Weight = 2,
                    RawPenalty = 2,
                    AppliedPenalty = 2,
                    MaxPenalty = 10
                }
            ]
        };

        return new UiPathAnalysisReportBuilder().Build(project, analysis, score, profile);
    }

    private static UiPathWorkflowInfo Workflow(string relativePath, int activityCount)
    {
        var analysis = new UiPathWorkflowAnalysis
        {
            FileName = Path.GetFileName(relativePath),
            RelativePath = relativePath
        };

        for (var index = 0; index < activityCount; index++)
        {
            analysis.Activities.Add(new UiPathActivityInfo
            {
                ActivityId = $"{relativePath}-{index}",
                Name = "Assign",
                DisplayName = "Assign value",
                TypeName = "Assign",
                Depth = 0,
                XamlFile = relativePath
            });
        }

        return new UiPathWorkflowInfo
        {
            Name = Path.GetFileName(relativePath),
            RelativePath = relativePath,
            FullPath = $"/tmp/Project/{relativePath}",
            Analysis = analysis
        };
    }

    private static UiPathStaticAnalysisResult Findings(params UiPathAnalysisFinding[] findings)
    {
        var result = new UiPathStaticAnalysisResult();
        result.Findings.AddRange(findings);
        return result;
    }

    private static UiPathAnalysisFinding Finding(string ruleId, RuleSeverity severity, string workflowPath, string? activityDisplayName = null)
    {
        return new UiPathAnalysisFinding
        {
            RuleId = ruleId,
            RuleName = "Test Rule",
            Severity = severity,
            Category = RuleCategory.Reliability,
            Message = activityDisplayName ?? "Message",
            WorkflowPath = workflowPath,
            ActivityDisplayName = activityDisplayName,
            Recommendation = "Fix it"
        };
    }
}

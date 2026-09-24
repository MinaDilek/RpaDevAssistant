using System.Net;
using System.Text;
using RpaDevAssistant.Core.Localization;

namespace RpaDevAssistant.Core.Reporting.Export;

public sealed class HtmlUiPathReportExporter : IUiPathReportExporter
{
    private readonly IRpaDevAssistantLocalizer localizer;

    public HtmlUiPathReportExporter(IRpaDevAssistantLocalizer? localizer = null)
    {
        this.localizer = localizer ?? new RpaDevAssistantLocalizer();
    }

    public UiPathReportExportFormat Format => UiPathReportExportFormat.Html;

    public UiPathReportExportResult Export(UiPathAnalysisReport report, string? locale = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        var reportLocale = localizer.NormalizeLocale(locale);

        return new UiPathReportExportResult
        {
            FileName = UiPathReportFileNameGenerator.Generate(report.ProjectName, report.GeneratedAtUtc, Format),
            ContentType = "text/html; charset=utf-8",
            Content = BuildHtml(report, reportLocale, localizer)
        };
    }

    private static string BuildHtml(UiPathAnalysisReport report, string locale, IRpaDevAssistantLocalizer localizer)
    {
        var html = new StringBuilder();
        var accentColor = report.Branding?.AccentColor ?? "#2458d3";
        html.AppendLine("<!doctype html>");
        html.AppendLine($"<html lang=\"{Encode(locale)}\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine($"<title>{Encode(report.ProductName)} - {Encode(L(localizer, locale, "Reports.Title"))}</title>");
        html.AppendLine("<style>");
        html.AppendLine($"body{{margin:0;font-family:Segoe UI,Arial,sans-serif;color:#1f2937;background:#f5f7fb}}main{{max-width:1180px;margin:0 auto;padding:32px}}header{{background:#fff;border-bottom:4px solid {accentColor};padding:28px 32px}}h1,h2,h3{{margin:0 0 12px}}p{{margin:4px 0;color:#596579}}.brand{{display:flex;align-items:center;gap:16px}}.brand img{{max-width:180px;max-height:72px;object-fit:contain}}.grid{{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px}}.card,section{{background:#fff;border:1px solid #d8dee8;border-radius:8px;padding:18px;margin:18px 0}}.metric{{font-size:28px;font-weight:700;color:{accentColor}}}.nav a{{margin-right:14px;color:{accentColor};text-decoration:none}}.badge{{display:inline-block;border-radius:999px;padding:3px 9px;font-size:12px;font-weight:700}}.Critical,.Error{{background:#fee4e2;color:#b42318}}.Warning{{background:#fff4cc;color:#8a5a00}}.Suggestion{{background:#e8f0ff;color:#2458d3}}.Info{{background:#e7f7ef;color:#137333}}table{{width:100%;border-collapse:collapse}}th,td{{padding:9px 10px;border-bottom:1px solid #e5eaf2;text-align:left;vertical-align:top}}th{{font-size:12px;text-transform:uppercase;color:#596579}}.finding{{border-top:1px solid #e5eaf2;padding:14px 0}}.finding:first-child{{border-top:0}}.kv{{display:grid;grid-template-columns:150px 1fr;gap:6px;margin-top:8px}}.kv div:nth-child(odd){{font-weight:700;color:#4b5563}}@media(max-width:700px){{main,header{{padding:18px}}.kv{{grid-template-columns:1fr}}}}");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<header>");
        html.AppendLine("<div class=\"brand\">");
        if (!string.IsNullOrWhiteSpace(report.Branding?.LogoDataUri))
        {
            html.AppendLine($"<img src=\"{Encode(report.Branding.LogoDataUri)}\" alt=\"{Encode(report.Branding.CompanyName ?? report.ProductName)}\">");
        }
        html.AppendLine("<div>");
        if (!string.IsNullOrWhiteSpace(report.Branding?.CompanyName))
        {
            html.AppendLine($"<p>{Encode(report.Branding.CompanyName)}</p>");
        }
        html.AppendLine($"<h1>{Encode(report.ProductName)}</h1>");
        html.AppendLine($"<h2>{Encode(L(localizer, locale, "Reports.Title"))}</h2>");
        html.AppendLine($"<p>{Encode(L(localizer, locale, "Reports.GeneratedAt"))} {Encode(report.GeneratedAtUtc.ToString("O"))} UTC · {Encode(L(localizer, locale, "Reports.ProductVersion"))} {Encode(report.ProductVersion)} · {Encode(L(localizer, locale, "Reports.Schema"))} {Encode(report.SchemaVersion)}</p>");
        html.AppendLine("</div></div>");
        html.AppendLine("</header>");
        html.AppendLine("<main>");
        html.AppendLine($"<nav class=\"nav\"><a href=\"#summary\">{Encode(L(localizer, locale, "Reports.Summary"))}</a><a href=\"#compliance\">{Encode(L(localizer, locale, "Reports.Compliance"))}</a><a href=\"#comparison\">{Encode(L(localizer, locale, "Reports.Comparison"))}</a><a href=\"#ai-review\">{Encode(L(localizer, locale, "Reports.AiReview"))}</a><a href=\"#fixes\">{Encode(L(localizer, locale, "Reports.FixSuggestions"))}</a><a href=\"#dependencies\">{Encode(L(localizer, locale, "Reports.DependencyAnalysis"))}</a><a href=\"#flowcharts\">{Encode(L(localizer, locale, "Reports.FlowchartAnalysis"))}</a><a href=\"#findings\">{Encode(L(localizer, locale, "Reports.Findings"))}</a><a href=\"#complexity\">{Encode(L(localizer, locale, "Reports.Complexity"))}</a><a href=\"#workflows\">{Encode(L(localizer, locale, "Reports.Workflows"))}</a><a href=\"#score-breakdown\">{Encode(L(localizer, locale, "Reports.ScoreBreakdown"))}</a></nav>");
        AppendSummary(html, report, locale, localizer);
        AppendCompliance(html, report, locale, localizer);
        AppendComparison(html, report, locale, localizer);
        AppendAiReview(html, report, locale, localizer);
        AppendFixSuggestions(html, report, locale, localizer);
        AppendDependencies(html, report, locale, localizer);
        AppendFlowcharts(html, report, locale, localizer);
        AppendFindings(html, report, locale, localizer);
        AppendComplexity(html, report, locale, localizer);
        AppendWorkflows(html, report, locale, localizer);
        AppendScoreBreakdown(html, report, locale, localizer);
        html.AppendLine("</main>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static void AppendSummary(StringBuilder html, UiPathAnalysisReport report, string locale, IRpaDevAssistantLocalizer localizer)
    {
        html.AppendLine("<section id=\"summary\">");
        html.AppendLine($"<h2>{Encode(L(localizer, locale, "Reports.ProjectSummary"))}</h2>");
        html.AppendLine("<div class=\"grid\">");
        AppendMetric(html, L(localizer, locale, "Reports.Project"), report.ProjectName ?? "Unknown");
        AppendMetric(html, L(localizer, locale, "Reports.Compatibility"), report.Compatibility ?? "Unknown");
        AppendMetric(html, L(localizer, locale, "Reports.Profile"), report.ProfileName);
        AppendMetric(html, L(localizer, locale, "Reports.Workflows"), report.WorkflowCount.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.Activities"), report.TotalActivityCount.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.QualityScore"), $"{report.QualityScore} / 100");
        AppendMetric(html, L(localizer, locale, "Reports.Grade"), report.Grade);
        AppendMetric(html, L(localizer, locale, "Reports.Findings"), report.Summary.TotalFindings.ToString());
        html.AppendLine("</div>");
        AppendExecutiveSummary(html, report, locale, localizer);
        html.AppendLine($"<h3>{Encode(L(localizer, locale, "Reports.FindingCounts"))}</h3>");
        html.AppendLine("<div class=\"grid\">");
        AppendMetric(html, L(localizer, locale, "Reports.Critical"), report.Summary.CriticalCount.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.Errors"), report.Summary.ErrorCount.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.Warnings"), report.Summary.WarningCount.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.Suggestions"), report.Summary.SuggestionCount.ToString());
        AppendMetric(html, "Info", report.Summary.InfoCount.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.WorkflowsWithFindings"), report.Summary.WorkflowsWithFindings.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.CleanWorkflows"), report.Summary.CleanWorkflows.ToString());
        html.AppendLine("</div>");
        AppendTopList(html, L(localizer, locale, "Reports.TopRules"), report.Summary.TopRules, locale, localizer);
        AppendTopList(html, L(localizer, locale, "Reports.TopCategories"), report.Summary.TopCategories, locale, localizer);
        html.AppendLine("</section>");
    }

    private static void AppendExecutiveSummary(StringBuilder html, UiPathAnalysisReport report, string locale, IRpaDevAssistantLocalizer localizer)
    {
        var summary = report.Summary.ExecutiveSummary;
        var values = new Dictionary<string, string?>
        {
            ["risk"] = L(localizer, locale, $"Reports.RiskLevel.{summary.RiskLevel}"),
            ["score"] = report.QualityScore.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["criticalErrors"] = summary.CriticalAndErrorFindings.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["workflows"] = summary.WorkflowsRequiringAttention.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        html.AppendLine("<div class=\"card\">");
        html.AppendLine($"<h3>{Encode(L(localizer, locale, "Reports.ExecutiveSummary"))}</h3>");
        html.AppendLine($"<p>{Encode(localizer.Get("Reports.ExecutiveNarrative", locale, values))}</p>");
        if (!string.IsNullOrWhiteSpace(summary.MostAffectedWorkflow))
        {
            html.AppendLine($"<p>{Encode(localizer.Get("Reports.MostAffectedWorkflowNarrative", locale, new Dictionary<string, string?> { ["workflow"] = summary.MostAffectedWorkflow, ["count"] = summary.MostAffectedWorkflowFindingCount.ToString(System.Globalization.CultureInfo.InvariantCulture) }))}</p>");
        }

        if (summary.PriorityRuleIds.Count > 0)
        {
            html.AppendLine($"<p><strong>{Encode(L(localizer, locale, "Reports.PriorityRules"))}:</strong> {Encode(string.Join(", ", summary.PriorityRuleIds))}</p>");
        }

        html.AppendLine("</div>");
    }

    private static void AppendCompliance(StringBuilder html, UiPathAnalysisReport report, string locale, IRpaDevAssistantLocalizer localizer)
    {
        if (report.Compliance is null)
        {
            return;
        }

        var compliance = report.Compliance;
        html.AppendLine("<section id=\"compliance\">");
        html.AppendLine($"<h2>{Encode(L(localizer, locale, "Reports.Compliance"))}</h2>");
        html.AppendLine("<div class=\"grid\">");
        AppendMetric(html, L(localizer, locale, "Reports.Standard"), compliance.StandardName);
        AppendMetric(html, L(localizer, locale, "Reports.ComplianceRate"), $"{compliance.CompliancePercentage:0.##}%");
        AppendMetric(html, L(localizer, locale, "Reports.CompliantRules"), compliance.CompliantRuleCount.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.NonCompliantRules"), compliance.NonCompliantRuleCount.ToString());
        html.AppendLine("</div>");
        if (compliance.Violations.Count > 0)
        {
            html.AppendLine($"<table><thead><tr><th>{Encode(L(localizer, locale, "Reports.Rule"))}</th><th>{Encode(L(localizer, locale, "Reports.Severity"))}</th><th>{Encode(L(localizer, locale, "Reports.Findings"))}</th><th>{Encode(L(localizer, locale, "Reports.Occurrences"))}</th></tr></thead><tbody>");
            foreach (var violation in compliance.Violations)
            {
                var ruleName = LocalizeRuleText(localizer, violation.Source, violation.RuleId, "Name", locale, violation.RuleName);
                html.AppendLine($"<tr><td>{Encode(violation.RuleId)} {Encode(ruleName)}</td><td>{Encode(violation.Severity.ToString())}</td><td>{violation.FindingCount}</td><td>{violation.OccurrenceCount}</td></tr>");
            }
            html.AppendLine("</tbody></table>");
        }
        html.AppendLine("</section>");
    }

    private static void AppendComparison(StringBuilder html, UiPathAnalysisReport report, string locale, IRpaDevAssistantLocalizer localizer)
    {
        if (report.Comparison is null)
        {
            return;
        }

        var comparison = report.Comparison;
        html.AppendLine("<section id=\"comparison\">");
        html.AppendLine($"<h2>{Encode(L(localizer, locale, "Reports.Comparison"))}</h2>");
        html.AppendLine("<div class=\"grid\">");
        AppendMetric(html, L(localizer, locale, "Reports.ScoreDelta"), comparison.ScoreDelta.ToString("+0;-0;0"));
        AppendMetric(html, L(localizer, locale, "Reports.FindingDelta"), comparison.TotalFindingDelta.ToString("+0;-0;0"));
        AppendMetric(html, L(localizer, locale, "Reports.NewFindings"), comparison.NewFindings.Count.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.ResolvedFindings"), comparison.ResolvedFindings.Count.ToString());
        html.AppendLine("</div>");
        html.AppendLine($"<p>{Encode(comparison.BaselineSnapshotId)} → {Encode(comparison.TargetSnapshotId)} · {Encode(comparison.GradeBefore ?? "-")} → {Encode(comparison.GradeAfter ?? "-")}</p>");
        html.AppendLine("</section>");
    }

    private static void AppendAiReview(StringBuilder html, UiPathAnalysisReport report, string locale, IRpaDevAssistantLocalizer localizer)
    {
        if (report.AiReview is null)
        {
            return;
        }

        var review = report.AiReview;
        html.AppendLine("<section id=\"ai-review\">");
        html.AppendLine($"<h2>{Encode(L(localizer, locale, "Reports.AiReview"))}</h2>");
        html.AppendLine($"<p>{Encode(review.Summary)}</p>");
        if (!review.IsSuccess)
        {
            AppendOptional(html, L(localizer, locale, "Reports.Status"), review.ErrorMessage);
        }
        foreach (var issue in review.Issues)
        {
            html.AppendLine($"<article class=\"finding\"><h3>{Encode(issue.Title)}</h3><p>{Encode(issue.Description)}</p><p><strong>{Encode(L(localizer, locale, "Reports.Recommendation"))}:</strong> {Encode(issue.Recommendation)}</p></article>");
        }
        html.AppendLine("</section>");
    }

    private static void AppendFixSuggestions(StringBuilder html, UiPathAnalysisReport report, string locale, IRpaDevAssistantLocalizer localizer)
    {
        if (report.FixSuggestions is null)
        {
            return;
        }

        html.AppendLine("<section id=\"fixes\">");
        html.AppendLine($"<h2>{Encode(L(localizer, locale, "Reports.FixSuggestions"))}</h2>");
        html.AppendLine($"<p>{report.FixSuggestions.Suggestions.Count} / {report.FixSuggestions.FixableFindings}</p>");
        foreach (var suggestion in report.FixSuggestions.Suggestions)
        {
            html.AppendLine($"<article class=\"finding\"><h3>{Encode(suggestion.RuleId)} {Encode(suggestion.Title)}</h3><p>{Encode(suggestion.Description)}</p><div class=\"kv\">");
            AppendOptional(html, L(localizer, locale, "Reports.Workflow"), suggestion.WorkflowPath);
            AppendOptional(html, L(localizer, locale, "Reports.CurrentState"), suggestion.CurrentState);
            AppendOptional(html, L(localizer, locale, "Reports.ProposedState"), suggestion.ProposedState);
            AppendOptional(html, L(localizer, locale, "Reports.Risk"), suggestion.RiskLevel.ToString());
            html.AppendLine("</div></article>");
        }
        html.AppendLine("</section>");
    }

    private static void AppendDependencies(StringBuilder html, UiPathAnalysisReport report, string locale, IRpaDevAssistantLocalizer localizer)
    {
        html.AppendLine("<section id=\"dependencies\">");
        html.AppendLine($"<h2>{Encode(L(localizer, locale, "Reports.DependencyAnalysis"))}</h2>");
        if (report.DependencyAnalysis is null)
        {
            html.AppendLine($"<p>{Encode(L(localizer, locale, "Reports.NoDependencyAnalysis"))}</p>");
            html.AppendLine("</section>");
            return;
        }

        html.AppendLine("<div class=\"grid\">");
        AppendMetric(html, L(localizer, locale, "Reports.Dependencies"), report.DependencyAnalysis.TotalDependencies.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.UiPathDependencies"), report.DependencyAnalysis.UiPathDependencies.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.ThirdPartyDependencies"), report.DependencyAnalysis.ThirdPartyDependencies.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.PossiblyUnused"), report.DependencyAnalysis.PossiblyUnusedDependencies.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.PotentialConflicts"), report.DependencyAnalysis.PotentialConflicts.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.ModernClassicMode"), report.DependencyAnalysis.ModernClassicMode.ToString());
        html.AppendLine("</div>");

        html.AppendLine($"<table><thead><tr><th>{Encode(L(localizer, locale, "Reports.Package"))}</th><th>{Encode(L(localizer, locale, "Reports.Version"))}</th><th>{Encode(L(localizer, locale, "Reports.Category"))}</th><th>{Encode(L(localizer, locale, "Reports.Usage"))}</th><th>{Encode(L(localizer, locale, "Reports.Risk"))}</th><th>{Encode(L(localizer, locale, "Reports.UsedByWorkflows"))}</th><th>{Encode(L(localizer, locale, "Reports.Notes"))}</th></tr></thead><tbody>");
        foreach (var package in report.DependencyAnalysis.Packages)
        {
            var notes = LocalizeDependencyNotes(package, locale, localizer);
            html.AppendLine($"<tr><td>{Encode(package.Name)}</td><td>{Encode(package.DeclaredVersion ?? string.Empty)}</td><td>{Encode(package.Category.ToString())}</td><td>{Encode(package.UsageStatus.ToString())}</td><td>{Encode(package.RiskLevel.ToString())}</td><td>{Encode(string.Join(", ", package.UsedByWorkflows.Take(5)))}</td><td>{Encode(notes ?? string.Empty)}</td></tr>");
        }

        html.AppendLine("</tbody></table>");
        html.AppendLine("</section>");
    }

    private static void AppendFlowcharts(StringBuilder html, UiPathAnalysisReport report, string locale, IRpaDevAssistantLocalizer localizer)
    {
        html.AppendLine("<section id=\"flowcharts\">");
        html.AppendLine($"<h2>{Encode(L(localizer, locale, "Reports.FlowchartAnalysis"))}</h2>");
        if (report.FlowchartAnalysis is null)
        {
            html.AppendLine($"<p>{Encode(L(localizer, locale, "Reports.NoFlowchartAnalysis"))}</p>");
            html.AppendLine("</section>");
            return;
        }

        html.AppendLine("<div class=\"grid\">");
        AppendMetric(html, L(localizer, locale, "Reports.FlowchartWorkflows"), report.FlowchartAnalysis.FlowchartWorkflowCount.ToString());
        AppendMetric(html, "Root Flowchart", report.FlowchartAnalysis.RootFlowchartWorkflowCount.ToString());
        AppendMetric(html, "Nested Flowchart", report.FlowchartAnalysis.NestedFlowchartWorkflowCount.ToString());
        AppendMetric(html, "Total Flowchart", report.FlowchartAnalysis.TotalFlowchartCount.ToString());
        AppendMetric(html, "Sequence", report.FlowchartAnalysis.SequenceWorkflowCount.ToString());
        AppendMetric(html, "State Machine", report.FlowchartAnalysis.StateMachineWorkflowCount.ToString());
        AppendMetric(html, "Mixed", report.FlowchartAnalysis.MixedWorkflowCount.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.SafeConversions"), report.FlowchartAnalysis.SafeConversionCount.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.RequiresReview"), report.FlowchartAnalysis.RequiresReviewCount.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.Complex"), report.FlowchartAnalysis.ComplexCount.ToString());
        AppendMetric(html, L(localizer, locale, "Reports.NotSupported"), report.FlowchartAnalysis.NotSupportedCount.ToString());
        html.AppendLine("</div>");

        var flowcharts = report.FlowchartAnalysis.Workflows
            .Where(workflow => workflow.ContainsFlowchart)
            .OrderBy(workflow => workflow.WorkflowPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (flowcharts.Length == 0)
        {
            html.AppendLine($"<p>{Encode(L(localizer, locale, "Reports.NoFlowchartWorkflows"))}</p>");
            html.AppendLine("</section>");
            return;
        }

        html.AppendLine($"<table><thead><tr><th>{Encode(L(localizer, locale, "Reports.Workflow"))}</th><th>{Encode(L(localizer, locale, "Reports.Structure"))}</th><th>{Encode(L(localizer, locale, "Reports.Nodes"))}</th><th>{Encode(L(localizer, locale, "Reports.DecisionCount"))}</th><th>{Encode(L(localizer, locale, "Reports.Switches"))}</th><th>{Encode(L(localizer, locale, "Reports.Cycles"))}</th><th>{Encode(L(localizer, locale, "Reports.Convertibility"))}</th><th>{Encode(L(localizer, locale, "Reports.Confidence"))}</th></tr></thead><tbody>");
        foreach (var workflow in flowcharts)
        {
            html.AppendLine($"<tr><td>{Encode(workflow.WorkflowPath)}</td><td>{Encode(workflow.IsRootFlowchart ? workflow.StructureType.ToString() : $"{workflow.StructureType} + Flowchart")}</td><td>{workflow.NodeCount}</td><td>{workflow.DecisionCount}</td><td>{workflow.SwitchCount}</td><td>{Encode(workflow.HasCycles ? L(localizer, locale, "Reports.Yes") : L(localizer, locale, "Reports.No"))}</td><td>{Encode(LocalizeFlowchartLevel(localizer, locale, workflow.ConversionLevel?.ToString()))}</td><td>{Encode(LocalizeFlowchartConfidence(localizer, locale, workflow.Confidence?.ToString()))}</td></tr>");
        }

        html.AppendLine("</tbody></table>");
        html.AppendLine("</section>");
    }

    private static void AppendComplexity(StringBuilder html, UiPathAnalysisReport report, string locale, IRpaDevAssistantLocalizer localizer)
    {
        html.AppendLine("<section id=\"complexity\">");
        html.AppendLine($"<h2>{Encode(L(localizer, locale, "Reports.WorkflowComplexity"))}</h2>");
        html.AppendLine("<div class=\"grid\">");
        foreach (var item in report.ComplexityDistribution)
        {
            AppendMetric(html, LocalizeComplexityLevel(localizer, locale, item.Name), item.Count.ToString());
        }

        html.AppendLine("</div>");
        html.AppendLine($"<h3>{Encode(L(localizer, locale, "Reports.TopComplexWorkflows"))}</h3>");
        html.AppendLine($"<table><thead><tr><th>{Encode(L(localizer, locale, "Reports.Workflow"))}</th><th>{Encode(L(localizer, locale, "Reports.ExecutableActivities"))}</th><th>{Encode(L(localizer, locale, "Reports.MaxNestingDepth"))}</th><th>{Encode(L(localizer, locale, "Reports.DecisionCount"))}</th><th>{Encode(L(localizer, locale, "Reports.LoopCount"))}</th><th>{Encode(L(localizer, locale, "Reports.ComplexityScore"))}</th><th>{Encode(L(localizer, locale, "Reports.ComplexityLevel"))}</th></tr></thead><tbody>");
        foreach (var workflow in report.TopComplexWorkflows)
        {
            html.AppendLine($"<tr><td>{Encode(workflow.WorkflowPath)}</td><td>{workflow.ExecutableActivities}</td><td>{workflow.MaxNestingDepth}</td><td>{workflow.DecisionCount}</td><td>{workflow.LoopCount}</td><td>{workflow.ComplexityScore}</td><td>{Encode(LocalizeComplexityLevel(localizer, locale, workflow.ComplexityLevel.ToString()))}</td></tr>");
        }

        html.AppendLine("</tbody></table>");
        html.AppendLine("</section>");
    }

    private static void AppendFindings(StringBuilder html, UiPathAnalysisReport report, string locale, IRpaDevAssistantLocalizer localizer)
    {
        html.AppendLine("<section id=\"findings\">");
        html.AppendLine($"<h2>{Encode(L(localizer, locale, "Reports.Findings"))}</h2>");
        if (report.Findings.Count == 0)
        {
            html.AppendLine($"<p>{Encode(L(localizer, locale, "Reports.NoIssues"))}</p>");
            html.AppendLine("</section>");
            return;
        }

        foreach (var finding in report.Findings)
        {
            var ruleName = LocalizeRuleText(localizer, finding.Source, finding.RuleId, "Name", locale, finding.RuleName);
            var message = LocalizeRuleText(localizer, finding.Source, finding.RuleId, "Message", locale, finding.Message);
            var recommendation = finding.Recommendation is null ? null : LocalizeRuleText(localizer, finding.Source, finding.RuleId, "Recommendation", locale, finding.Recommendation);
            html.AppendLine("<article class=\"finding\">");
            html.AppendLine($"<h3><span class=\"badge {Encode(finding.Severity.ToString())}\">{Encode(finding.Severity.ToString())}</span> {Encode(finding.RuleId)} {Encode(ruleName)}</h3>");
            html.AppendLine($"<p>{Encode(message)}</p>");
            html.AppendLine("<div class=\"kv\">");
            AppendOptional(html, L(localizer, locale, "Reports.Category"), finding.Category.ToString());
            AppendOptional(html, L(localizer, locale, "Reports.Source"), finding.Source);
            AppendOptional(html, L(localizer, locale, "Reports.Workflow"), finding.WorkflowPath);
            AppendOptional(html, L(localizer, locale, "Reports.Activity"), finding.ActivityDisplayName ?? finding.ActivityName);
            AppendOptional(html, L(localizer, locale, "Reports.CurrentValue"), finding.CurrentValue);
            AppendOptional(html, L(localizer, locale, "Reports.Recommendation"), recommendation);
            html.AppendLine("</div>");
            html.AppendLine("</article>");
        }

        html.AppendLine("</section>");
    }

    private static void AppendWorkflows(StringBuilder html, UiPathAnalysisReport report, string locale, IRpaDevAssistantLocalizer localizer)
    {
        html.AppendLine("<section id=\"workflows\">");
        html.AppendLine($"<h2>{Encode(L(localizer, locale, "Reports.WorkflowSummary"))}</h2>");
        html.AppendLine($"<table><thead><tr><th>{Encode(L(localizer, locale, "Reports.Workflow"))}</th><th>{Encode(L(localizer, locale, "Reports.Status"))}</th><th>{Encode(L(localizer, locale, "Reports.Activities"))}</th><th>{Encode(L(localizer, locale, "Reports.Findings"))}</th><th>{Encode(L(localizer, locale, "Reports.Complexity"))}</th><th>{Encode(L(localizer, locale, "Reports.ComplexityScore"))}</th><th>{Encode(L(localizer, locale, "Reports.Errors"))}</th><th>{Encode(L(localizer, locale, "Reports.Warnings"))}</th><th>{Encode(L(localizer, locale, "Reports.Suggestions"))}</th></tr></thead><tbody>");
        foreach (var workflow in report.WorkflowSummaries)
        {
            html.AppendLine($"<tr><td>{Encode(workflow.RelativePath)}</td><td>{Encode(workflow.Status)}</td><td>{workflow.ActivityCount}</td><td>{workflow.FindingCount}</td><td>{Encode(workflow.Complexity is null ? string.Empty : LocalizeComplexityLevel(localizer, locale, workflow.Complexity.ComplexityLevel.ToString()))}</td><td>{workflow.Complexity?.ComplexityScore.ToString() ?? string.Empty}</td><td>{workflow.ErrorCount}</td><td>{workflow.WarningCount}</td><td>{workflow.SuggestionCount}</td></tr>");
        }

        html.AppendLine("</tbody></table>");
        html.AppendLine("</section>");
    }

    private static void AppendScoreBreakdown(StringBuilder html, UiPathAnalysisReport report, string locale, IRpaDevAssistantLocalizer localizer)
    {
        html.AppendLine("<section id=\"score-breakdown\">");
        html.AppendLine($"<h2>{Encode(L(localizer, locale, "Reports.ScoreBreakdown"))}</h2>");
        html.AppendLine($"<table><thead><tr><th>{Encode(L(localizer, locale, "Reports.Rule"))}</th><th>{Encode(L(localizer, locale, "Reports.Severity"))}</th><th>{Encode(L(localizer, locale, "Reports.Findings"))}</th><th>{Encode(L(localizer, locale, "Reports.Weight"))}</th><th>{Encode(L(localizer, locale, "Reports.RawPenalty"))}</th><th>{Encode(L(localizer, locale, "Reports.AppliedPenalty"))}</th><th>{Encode(L(localizer, locale, "Reports.MaxPenalty"))}</th></tr></thead><tbody>");
        foreach (var item in report.ScoreBreakdown)
        {
            html.AppendLine($"<tr><td>{Encode(item.RuleId)} {Encode(LocalizeCustomText(item.RuleName, locale) ?? item.RuleName)}</td><td>{Encode(item.Severity.ToString())}</td><td>{item.FindingCount}</td><td>{item.Weight}</td><td>{item.RawPenalty}</td><td>{item.AppliedPenalty}</td><td>{item.MaxPenalty}</td></tr>");
        }

        html.AppendLine("</tbody></table>");
        html.AppendLine("</section>");
    }

    private static void AppendMetric(StringBuilder html, string label, string value)
    {
        html.AppendLine($"<div class=\"card\"><p>{Encode(label)}</p><div class=\"metric\">{Encode(value)}</div></div>");
    }

    private static void AppendTopList(StringBuilder html, string title, IReadOnlyList<UiPathReportCount> items, string locale, IRpaDevAssistantLocalizer localizer)
    {
        html.AppendLine($"<h3>{Encode(title)}</h3>");
        if (items.Count == 0)
        {
            html.AppendLine($"<p>{Encode(L(localizer, locale, "Reports.NoIssues"))}</p>");
            return;
        }

        html.AppendLine("<table><tbody>");
        foreach (var item in items)
        {
            html.AppendLine($"<tr><td>{Encode(item.Name)}</td><td>{item.Count}</td></tr>");
        }

        html.AppendLine("</tbody></table>");
    }

    private static void AppendOptional(StringBuilder html, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        html.AppendLine($"<div>{Encode(label)}</div><div>{Encode(value)}</div>");
    }

    private static string? LocalizeDependencyNotes(RpaDevAssistant.Core.Dependencies.UiPathDependencyAnalysis package, string locale, IRpaDevAssistantLocalizer localizer)
    {
        if (package.UsageStatus == RpaDevAssistant.Core.Dependencies.UiPathDependencyUsageStatus.PossiblyUnused)
        {
            return localizer.Get("Ask.DependencyPossiblyUnusedReason", locale);
        }

        return package.Findings.Count > 0 ? string.Join("; ", package.Findings) : package.Notes;
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string L(IRpaDevAssistantLocalizer localizer, string locale, string key)
    {
        return localizer.Get(key, locale);
    }

    private static string LocalizeRuleText(IRpaDevAssistantLocalizer localizer, string source, string ruleId, string field, string locale, string fallback)
    {
        if (source.Equals("Custom", StringComparison.OrdinalIgnoreCase))
        {
            return LocalizeCustomText(fallback, locale) ?? fallback;
        }

        return localizer.Get($"Rules.{ruleId}.{field}", locale, fallback: fallback);
    }

    private static string? LocalizeCustomText(string? packedText, string locale)
    {
        if (string.IsNullOrWhiteSpace(packedText))
        {
            return packedText;
        }

        var parts = packedText.Split('\u001f');
        if (parts.Length != 3)
        {
            return packedText;
        }

        var fallback = parts[0];
        var english = parts[1];
        var turkish = parts[2];
        if (locale.Equals("tr", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(turkish))
        {
            return turkish;
        }

        if (locale.Equals("en", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(english))
        {
            return english;
        }

        return !string.IsNullOrWhiteSpace(english) ? english : fallback;
    }

    private static string LocalizeComplexityLevel(IRpaDevAssistantLocalizer localizer, string locale, string level)
    {
        return localizer.Get($"Complexity.Level.{level.Replace(" ", string.Empty, StringComparison.Ordinal)}", locale, fallback: level);
    }

    private static string LocalizeFlowchartLevel(IRpaDevAssistantLocalizer localizer, string locale, string? level)
    {
        return string.IsNullOrWhiteSpace(level)
            ? "Unknown"
            : localizer.Get($"Flowchart.Level.{level.Replace(" ", string.Empty, StringComparison.Ordinal)}", locale, fallback: level);
    }

    private static string LocalizeFlowchartConfidence(IRpaDevAssistantLocalizer localizer, string locale, string? confidence)
    {
        return string.IsNullOrWhiteSpace(confidence)
            ? "Unknown"
            : localizer.Get($"Flowchart.Confidence.{confidence.Replace(" ", string.Empty, StringComparison.Ordinal)}", locale, fallback: confidence);
    }
}

using System.Text;
using RpaDevAssistant.Core.Localization;

namespace RpaDevAssistant.Core.Ai;

public sealed class UiPathAiPromptBuilder : IUiPathAiPromptBuilder
{
    public UiPathAiPrompt Build(UiPathAiReviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UiPathAiPrompt
        {
            Scope = request.ReviewScope,
            WorkflowPath = request.SelectedWorkflow,
            Locale = request.Locale,
            SystemInstructions = BuildSystemInstructions(request.Locale),
            UserContext = BuildUserContext(request)
        };
    }

    private static string BuildSystemInstructions(string? locale)
    {
        var languageInstruction = SupportedLocale.Normalize(locale) == SupportedLocale.Turkish
            ? "Respond in Turkish. Keep UiPath technical terms and identifiers unchanged."
            : "Respond in English.";

        return """
        You are reviewing a UiPath automation project.
        LANGUAGE_INSTRUCTION
        Deterministic findings are authoritative evidence and must not be deleted, contradicted, or rewritten.
        Do not invent activities, workflows, dependencies, databases, credentials, or integrations.
        Clearly distinguish evidence from inference.
        Put only facts supported by supplied deterministic findings in evidence fields.
        Put conclusions and recommendations in interpretation fields.
        If evidence is insufficient, say so.
        Prioritize actionable recommendations.
        Do not expose secrets. Values marked [REDACTED] must remain redacted.
        Return only structured JSON matching this shape:
        {
          "summary": "string",
          "interpretation": "conclusion based on supplied evidence",
          "riskLevel": "Low|Medium|High|Critical",
          "strengths": ["string"],
          "issues": [
            {
              "title": "string",
              "severity": "Info|Low|Medium|High|Critical",
              "description": "string",
              "evidence": "string",
              "evidenceItems": [{ "statement": "supported fact", "ruleId": "RPA001", "workflowPath": "Main.xaml", "activityName": "Delay" }],
              "interpretation": "conclusion based on evidence",
              "recommendation": "string",
              "workflowPath": "string or null",
              "relatedRuleIds": ["RPA001"]
            }
          ],
          "recommendations": ["string"],
          "architectureObservations": ["string"],
          "confidence": 0.0
        }
        """.Replace("LANGUAGE_INSTRUCTION", languageInstruction, StringComparison.Ordinal);
    }

    private static string BuildUserContext(UiPathAiReviewRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"ReviewScope: {request.ReviewScope}");
        builder.AppendLine($"ProjectName: {request.ProjectName ?? "Unknown"}");
        builder.AppendLine($"Compatibility: {request.Compatibility ?? "Unknown"}");
        builder.AppendLine($"IsReFramework: {request.IsReFramework}");
        builder.AppendLine($"WorkflowCount: {request.WorkflowCount}");
        builder.AppendLine($"TotalActivityCount: {request.TotalActivityCount}");
        if (!string.IsNullOrWhiteSpace(request.SelectedWorkflow))
        {
            builder.AppendLine($"SelectedWorkflow: {request.SelectedWorkflow}");
        }

        builder.AppendLine();
        builder.AppendLine("Dependencies:");
        foreach (var dependency in request.Dependencies)
        {
            builder.AppendLine($"- {dependency.Name} {dependency.Version}");
        }

        builder.AppendLine();
        builder.AppendLine("Workflows:");
        foreach (var workflow in request.Workflows)
        {
            builder.AppendLine($"- {workflow.RelativePath} ({workflow.ActivityCount} activities)");
            foreach (var argument in workflow.Arguments)
            {
                builder.AppendLine($"  Argument: {argument}");
            }

            foreach (var activity in workflow.Activities)
            {
                builder.AppendLine($"  Activity: {activity}");
            }

            foreach (var invokeReference in workflow.InvokeReferences)
            {
                builder.AppendLine($"  InvokeReference: {invokeReference}");
            }

            if (workflow.IsTruncated)
            {
                builder.AppendLine("  [TRUNCATED]");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Deterministic Findings:");
        foreach (var finding in request.DeterministicFindings)
        {
            builder.AppendLine($"- {finding.RuleId} {finding.RuleName} [{finding.Severity}] Workflow={finding.WorkflowPath ?? "Project"} Activity={finding.ActivityDisplayName ?? finding.ActivityName ?? "n/a"} Message={finding.Message}");
        }

        if (!string.IsNullOrWhiteSpace(request.AdditionalInstructions))
        {
            builder.AppendLine();
            builder.AppendLine("Additional Instructions:");
            builder.AppendLine(request.AdditionalInstructions);
        }

        return builder.ToString();
    }
}

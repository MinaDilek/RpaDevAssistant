using System.Text;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Localization;

namespace RpaDevAssistant.Core.ProjectAssistant;

public sealed class UiPathProjectAssistantPromptBuilder : IUiPathProjectAssistantPromptBuilder
{
    public UiPathProjectAssistantPrompt Build(UiPathProjectQuestion request, UiPathProjectAnalysisResult analysis, IReadOnlyList<UiPathProjectEvidence> evidence)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Question: {request.Question}");
        builder.AppendLine($"Project: {analysis.ProjectName ?? "Unknown"}");
        builder.AppendLine($"Workflows: {analysis.WorkflowCount}");
        builder.AppendLine($"Activities: {analysis.TotalActivityCount}");
        builder.AppendLine($"QualityScore: {analysis.QualityScore.Score} ({analysis.QualityScore.Grade})");
        if (!string.IsNullOrWhiteSpace(request.PreferredWorkflowPath))
        {
            builder.AppendLine($"PreferredWorkflow: {request.PreferredWorkflowPath}");
        }

        builder.AppendLine();
        builder.AppendLine("Relevant Evidence:");
        foreach (var item in evidence)
        {
            builder.AppendLine($"- Type: {item.Type}");
            Append(builder, "Workflow", item.WorkflowPath);
            Append(builder, "Activity", item.ActivityDisplayName ?? item.ActivityName);
            Append(builder, "Rule", item.RuleId);
            Append(builder, "Property", item.PropertyName);
            Append(builder, "Value", item.Value);
            Append(builder, "Description", item.Description);
            builder.AppendLine($"  Relevance: {item.RelevanceScore:0.##}");
        }

        builder.AppendLine();
        builder.AppendLine("Return structured JSON with: answer, interpretation, confidence, answerType, relatedWorkflows, relatedActivities, relatedRuleIds, reasoningSummary.");

        return new UiPathProjectAssistantPrompt
        {
            Question = request.Question,
            SystemInstructions = """
                You are the Ask Project assistant for RPA Dev Assistant.
                LANGUAGE_INSTRUCTION
                Answer only from the provided project evidence.
                Do not invent workflows, activities, dependencies, findings, execution results, or file contents.
                If evidence is insufficient, say so explicitly.
                Separate facts from interpretation.
                Evidence is supplied by the application and must not be rewritten or invented.
                Put conclusions only in interpretation; identifiers must reference supplied evidence.
                Prefer concise actionable answers.
                Mention workflow names when relevant.
                Do not expose redacted secrets.
                Do not claim to have executed the UiPath project.
                Do not include internal chain-of-thought. Provide only a short reasoningSummary.
                Return only structured JSON.
                """.Replace("LANGUAGE_INSTRUCTION", SupportedLocale.Normalize(request.Locale) == SupportedLocale.Turkish
                    ? "Respond in Turkish. Keep UiPath technical terms and identifiers unchanged."
                    : "Respond in English.", StringComparison.Ordinal),
            UserContext = builder.ToString(),
            Locale = request.Locale
        };
    }

    private static void Append(StringBuilder builder, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"  {label}: {value}");
        }
    }
}

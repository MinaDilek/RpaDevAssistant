using System.Text;
using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Localization;

namespace RpaDevAssistant.Core.Fixes;

public sealed class UiPathAiFixPromptBuilder : IUiPathAiFixPromptBuilder
{
    private readonly ISecretRedactor redactor;

    public UiPathAiFixPromptBuilder(ISecretRedactor redactor)
    {
        this.redactor = redactor;
    }

    public UiPathAiFixPrompt Build(UiPathFixContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Project: {context.Project.ProjectName ?? "Unknown"}");
        builder.AppendLine($"Workflow: {context.Workflow?.RelativePath ?? context.Finding.WorkflowPath ?? "Project"}");
        builder.AppendLine($"Finding: {context.Finding.RuleId} {context.Finding.RuleName}");
        builder.AppendLine($"Message: {context.Finding.Message}");
        Append(builder, "Activity", context.Activity is null ? context.Finding.ActivityName : $"{context.Activity.DisplayName} ({context.Activity.Name})");
        Append(builder, "Property", context.Finding.PropertyName);
        Append(builder, "CurrentValue", redactor.Redact(context.Finding.PropertyName ?? string.Empty, context.Finding.CurrentValue));

        builder.AppendLine();
        builder.AppendLine("NearbyActivities:");
        foreach (var activity in context.NearbyActivities.Take(5))
        {
            builder.AppendLine($"- {activity.DisplayName} ({activity.Name})");
            foreach (var property in activity.Properties.Take(4))
            {
                builder.AppendLine($"  {property.Key}: {redactor.Redact(property.Key, property.Value)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("RelatedFindings:");
        foreach (var finding in context.RelatedFindings.Take(8))
        {
            builder.AppendLine($"- {finding.RuleId} {finding.RuleName}: {finding.Message}");
        }

        return new UiPathAiFixPrompt
        {
            SystemInstructions = """
                Suggest changes only from provided evidence.
                LANGUAGE_INSTRUCTION
                Do not claim the fix is guaranteed.
                Do not invent UiPath activities.
                Prefer minimal changes.
                Preserve existing business behavior.
                Do not output secrets.
                Do not generate executable code that changes files.
                Return a structured suggestion only.
                Clearly state assumptions.
                If evidence is insufficient, provide a general recommendation instead of inventing specifics.
                """.Replace("LANGUAGE_INSTRUCTION", SupportedLocale.Normalize(context.Locale) == SupportedLocale.Turkish
                    ? "Respond in Turkish. Keep UiPath technical terms and identifiers unchanged."
                    : "Respond in English.", StringComparison.Ordinal),
            UserContext = builder.ToString(),
            Locale = context.Locale
        };
    }

    private static void Append(StringBuilder builder, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"{label}: {value}");
        }
    }
}

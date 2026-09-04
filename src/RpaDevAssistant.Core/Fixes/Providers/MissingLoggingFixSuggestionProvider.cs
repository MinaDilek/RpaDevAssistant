namespace RpaDevAssistant.Core.Fixes.Providers;

public sealed class MissingLoggingFixSuggestionProvider : UiPathFixSuggestionProviderBase
{
    public override IReadOnlyCollection<string> SupportedRuleIds { get; } = ["RPA008"];

    public override UiPathFixSuggestion? Suggest(UiPathFixContext context)
    {
        const string before = "Workflow has no Log Message activity.";
        const string after = "Add Log Message activities at workflow start, critical actions, completion, and error paths.";
        return new UiPathFixSuggestion
        {
            Id = SuggestionId(context),
            RuleId = context.Finding.RuleId,
            Title = "Add meaningful workflow logging",
            Description = "Add Log Message activities at important workflow boundaries.",
            FixType = UiPathFixSuggestionType.ActivityInsertion,
            Fixability = UiPathFixability.Advisory,
            Confidence = UiPathFixConfidence.Medium,
            RiskLevel = UiPathFixRiskLevel.Low,
            WorkflowPath = context.Finding.WorkflowPath,
            BeforePreview = before,
            AfterPreview = after,
            PatchPreview = TextPreview(before, after),
            Explanation = "Without logging, production diagnosis and support handover become harder.",
            Steps = ["Add logging at workflow start/end.", "Log external calls and important business decisions.", "Avoid logging credentials, tokens, or personal data."],
            Risks = ["Overly verbose logging can make production logs noisy.", "Logging sensitive values can create security exposure."],
            ValidationNotes = ["Avoid logging sensitive values.", "Use consistent log levels and include useful business context."],
            RequiresUserInput = true,
            UserInputHints = ["Choose workflow boundaries and business events worth logging."],
            CanAutoApply = false
        };
    }
}

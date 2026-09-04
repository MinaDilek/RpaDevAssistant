namespace RpaDevAssistant.Core.Fixes.Providers;

public sealed class WorkflowNamingFixSuggestionProvider : UiPathFixSuggestionProviderBase
{
    public override IReadOnlyCollection<string> SupportedRuleIds { get; } = ["RPA006"];

    public override UiPathFixSuggestion? Suggest(UiPathFixContext context)
    {
        var currentName = context.Finding.WorkflowPath ?? context.Workflow?.RelativePath;
        if (string.IsNullOrWhiteSpace(currentName))
        {
            return null;
        }

        const string suggested = "Rename workflow to a descriptive PascalCase name.";
        return new UiPathFixSuggestion
        {
            Id = SuggestionId(context),
            RuleId = context.Finding.RuleId,
            Title = "Rename generic workflow file",
            Description = "Use a descriptive PascalCase workflow file name.",
            FixType = UiPathFixSuggestionType.NamingChange,
            Fixability = UiPathFixability.Previewable,
            Confidence = UiPathFixConfidence.Medium,
            RiskLevel = UiPathFixRiskLevel.Low,
            WorkflowPath = currentName,
            PropertyName = "WorkflowPath",
            CurrentValue = currentName,
            SuggestedValue = suggested,
            BeforePreview = currentName,
            AfterPreview = suggested,
            PatchPreview = PropertyPreview("WorkflowPath", currentName, suggested),
            Explanation = "Generic workflow names make navigation, reviews, and ownership harder as the project grows.",
            Steps = ["Choose a descriptive PascalCase workflow name.", "Update Invoke Workflow File references that point to the old file.", "Re-run analysis to verify references."],
            Risks = ["Existing Invoke Workflow File references may need to be updated.", "External documentation or tests may reference the old filename."],
            ValidationNotes = ["Rename references to this workflow before changing the file name.", "Verify Invoke Workflow File references after renaming."],
            RequiresUserInput = true,
            UserInputHints = ["Provide the intended workflow responsibility as a PascalCase name."],
            CanAutoApply = false
        };
    }
}

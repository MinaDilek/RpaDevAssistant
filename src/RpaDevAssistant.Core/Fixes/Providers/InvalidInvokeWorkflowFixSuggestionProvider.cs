namespace RpaDevAssistant.Core.Fixes.Providers;

public sealed class InvalidInvokeWorkflowFixSuggestionProvider : UiPathFixSuggestionProviderBase
{
    public override IReadOnlyCollection<string> SupportedRuleIds { get; } = ["RPA005"];

    public override UiPathFixSuggestion? Suggest(UiPathFixContext context)
    {
        var current = context.Finding.CurrentValue ?? context.Activity?.Properties.GetValueOrDefault("WorkflowFileName");
        if (string.IsNullOrWhiteSpace(current))
        {
            return null;
        }

        var similar = FileNameSimilarity.FindSimilar(current, context.Project.Workflows.Select(workflow => workflow.RelativePath));
        const string fallback = "Correct relative workflow path";
        var after = similar is null
            ? "Verify the referenced workflow path, restore the missing file, or update the relative path after rename/move."
            : $"WorkflowFileName = \"{similar}\"";

        return new UiPathFixSuggestion
        {
            Id = SuggestionId(context),
            RuleId = context.Finding.RuleId,
            Title = "Fix invalid Invoke Workflow reference",
            Description = "The referenced workflow path was not found in the project.",
            FixType = UiPathFixSuggestionType.PropertyChange,
            Fixability = UiPathFixability.Previewable,
            Confidence = similar is null ? UiPathFixConfidence.Medium : UiPathFixConfidence.High,
            RiskLevel = UiPathFixRiskLevel.Medium,
            WorkflowPath = context.Finding.WorkflowPath,
            ActivityId = context.Finding.ActivityId,
            ActivityName = context.Finding.ActivityName,
            ActivityDisplayName = context.Finding.ActivityDisplayName,
            PropertyName = "WorkflowFileName",
            CurrentValue = current,
            SuggestedValue = similar ?? fallback,
            BeforePreview = $"WorkflowFileName = \"{current}\"",
            AfterPreview = after,
            PatchPreview = similar is null
                ? TextPreview($"WorkflowFileName = \"{current}\"", after)
                : PropertyPreview("WorkflowFileName", current, similar),
            Explanation = "Broken workflow references fail at runtime or prevent intended workflow reuse. A similar existing workflow is suggested only when filename similarity is high.",
            Steps = ["Confirm which workflow should be invoked.", "Update the Invoke Workflow File path in UiPath Studio.", "Re-run analysis to verify the reference resolves."],
            Risks = ["Choosing the wrong workflow target can change business behavior.", "Renamed workflows may require multiple Invoke Workflow File references to be updated."],
            ValidationNotes = ["Verify the referenced workflow exists.", "Confirm the suggested path is the intended target before editing in UiPath Studio."],
            RequiresUserInput = true,
            UserInputHints = ["Select the intended workflow file."],
            CanAutoApply = false
        };
    }
}

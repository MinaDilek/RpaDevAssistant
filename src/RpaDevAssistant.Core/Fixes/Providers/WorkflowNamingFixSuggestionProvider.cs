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

        var directory = Path.GetDirectoryName(currentName)?.Replace('\\', '/');
        var suggestedFileName = "DescriptivePascalCaseName.xaml";
        var suggested = string.IsNullOrWhiteSpace(directory)
            ? suggestedFileName
            : $"{directory}/{suggestedFileName}";
        var normalizedCurrentName = NormalizePath(currentName);
        var callers = context.InvocationGraph.Edges
            .Where(edge => !edge.IsDynamicReference
                && edge.CalleeWorkflowPath is not null
                && NormalizePath(edge.CalleeWorkflowPath).Equals(normalizedCurrentName, StringComparison.OrdinalIgnoreCase))
            .Select(edge => NormalizePath(edge.CallerWorkflowPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var referenceNotes = callers.Length == 0
            ? ["No statically resolved Invoke Workflow File references were found. Dynamic or external references still require manual review."]
            : callers.Select(caller => $"Update the Invoke Workflow File reference in {caller}.").ToArray();

        return new UiPathFixSuggestion
        {
            Id = SuggestionId(context),
            RuleId = context.Finding.RuleId,
            Title = "Rename generic workflow file",
            Description = "Preview a descriptive PascalCase workflow file name and every statically resolved caller affected by the rename.",
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
            PatchPreview = new UiPathPatchPreview
            {
                Format = UiPathPatchPreviewFormat.PropertyChange,
                WorkflowPath = currentName,
                Description = "Workflow file rename preview. No file or caller reference is changed automatically.",
                Before = currentName,
                After = suggested,
                ChangedProperties =
                [
                    new UiPathChangedProperty { Name = "WorkflowPath", Before = currentName, After = suggested }
                ],
                Notes = referenceNotes
            },
            Explanation = "Generic workflow names make navigation, reviews, and ownership harder as the project grows.",
            Steps = ["Choose a descriptive PascalCase workflow name.", "Update Invoke Workflow File references that point to the old file.", "Re-run analysis to verify references."],
            Risks = ["Existing Invoke Workflow File references may need to be updated.", "External documentation or tests may reference the old filename."],
            ValidationNotes =
            [
                .. referenceNotes,
                "Search for dynamic and external references that cannot be resolved statically.",
                "Verify all Invoke Workflow File references after renaming."
            ],
            RequiresUserInput = true,
            UserInputHints = ["Provide the intended workflow responsibility as a PascalCase name."],
            CanAutoApply = false
        };
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('.', '/');
}

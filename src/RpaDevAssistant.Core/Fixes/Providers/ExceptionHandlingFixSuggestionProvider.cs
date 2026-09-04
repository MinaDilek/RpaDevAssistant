namespace RpaDevAssistant.Core.Fixes.Providers;

public sealed class ExceptionHandlingFixSuggestionProvider : UiPathFixSuggestionProviderBase
{
    public override IReadOnlyCollection<string> SupportedRuleIds { get; } = ["RPA002", "RPA003"];

    public override UiPathFixSuggestion? Suggest(UiPathFixContext context)
    {
        var isEmptyCatch = context.Finding.RuleId.Equals("RPA002", StringComparison.OrdinalIgnoreCase);
        var before = isEmptyCatch
            ? "Catch Exception\n<empty>"
            : "Catch Exception\n<no Log Message / Throw / Rethrow detected>";
        const string after = "Catch Exception\nLog Message: exception context\nRethrow // if exception should propagate";

        return new UiPathFixSuggestion
        {
            Id = SuggestionId(context),
            RuleId = context.Finding.RuleId,
            Title = isEmptyCatch ? "Handle empty Catch block" : "Avoid silently swallowing exceptions",
            Description = isEmptyCatch
                ? "Add meaningful handling to the empty Catch block."
                : "Add logging and decide whether the exception should propagate.",
            FixType = UiPathFixSuggestionType.ExceptionHandlingChange,
            Fixability = UiPathFixability.Advisory,
            Confidence = UiPathFixConfidence.Medium,
            RiskLevel = UiPathFixRiskLevel.High,
            WorkflowPath = context.Finding.WorkflowPath,
            ActivityId = context.Finding.ActivityId,
            ActivityName = context.Finding.ActivityName,
            ActivityDisplayName = context.Finding.ActivityDisplayName,
            BeforePreview = before,
            AfterPreview = after,
            PatchPreview = TextPreview(before, after),
            Explanation = "Unhandled or silently swallowed exceptions can hide failed transactions and make production recovery unreliable. The correct behavior depends on whether the exception is expected or should stop the current transaction.",
            Steps = ["Add a Log Message with exception context.", "Decide whether the exception should be rethrown, transformed, or explicitly handled.", "Verify transaction status behavior after the change."],
            Risks = ["Adding Rethrow when the exception is expected can change business flow.", "Handling without propagation can hide real failures if not designed carefully."],
            ValidationNotes = ["Review expected versus unexpected exception behavior.", "Test in UiPath Studio.", "Verify exception propagation and transaction status handling."],
            RequiresUserInput = true,
            UserInputHints = ["Decide whether this Catch handles an expected business exception or an unexpected application failure."],
            RequiresAi = false,
            CanAutoApply = false
        };
    }
}

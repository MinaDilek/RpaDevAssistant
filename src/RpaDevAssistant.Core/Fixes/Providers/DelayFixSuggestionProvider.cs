namespace RpaDevAssistant.Core.Fixes.Providers;

public sealed class DelayFixSuggestionProvider : UiPathFixSuggestionProviderBase
{
    public override IReadOnlyCollection<string> SupportedRuleIds { get; } = ["RPA001", "RPA004"];

    public override UiPathFixSuggestion? Suggest(UiPathFixContext context)
    {
        var duration = context.Finding.CurrentValue ?? context.Activity?.Properties.GetValueOrDefault("Duration");
        var before = string.IsNullOrWhiteSpace(duration) ? "Delay" : $"Delay {duration}";
        const string after = "Check App State / Retry Scope\nTarget: relevant UI/application state\nTimeout: configurable";

        return new UiPathFixSuggestion
        {
            Id = SuggestionId(context),
            RuleId = context.Finding.RuleId,
            Title = context.Finding.RuleId == "RPA004" ? "Replace long hard-coded delay" : "Replace fixed delay with state-based wait",
            Description = "Use a state-based wait instead of a fixed Delay activity.",
            FixType = UiPathFixSuggestionType.ActivityReplacement,
            Fixability = UiPathFixability.Advisory,
            Confidence = UiPathFixConfidence.Medium,
            RiskLevel = UiPathFixRiskLevel.Medium,
            WorkflowPath = context.Finding.WorkflowPath,
            ActivityId = context.Finding.ActivityId,
            ActivityName = context.Finding.ActivityName ?? context.Activity?.Name,
            ActivityDisplayName = context.Finding.ActivityDisplayName ?? context.Activity?.DisplayName,
            PropertyName = "Duration",
            CurrentValue = duration,
            BeforePreview = before,
            AfterPreview = after,
            PatchPreview = TextPreview(before, after),
            Explanation = "Fixed delays are sensitive to machine, network, and application performance and may create unstable automations.",
            Steps = ["Identify the expected UI or application state.", "Replace the fixed delay with Check App State, Retry Scope, Element Exists, or a timeout-based UI activity.", "Keep timeout values configurable."],
            Risks = ["The correct target state cannot be inferred safely from static XAML alone.", "Changing wait behavior can affect timing-sensitive workflows."],
            ValidationNotes = ["Verify the target state in UiPath Studio.", "Use configurable timeout values where possible."],
            RequiresUserInput = true,
            UserInputHints = ["Select the UI/application state that indicates readiness."],
            RequiresAi = false,
            CanAutoApply = false
        };
    }
}

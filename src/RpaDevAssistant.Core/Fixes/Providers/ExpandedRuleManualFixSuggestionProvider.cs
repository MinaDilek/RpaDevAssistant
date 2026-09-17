namespace RpaDevAssistant.Core.Fixes.Providers;

public sealed class ExpandedRuleManualFixSuggestionProvider : UiPathFixSuggestionProviderBase
{
    private static readonly Dictionary<string, ManualFixTemplate> Templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RPA017"] = new ManualFixTemplate(
            "Review selector idx usage",
            "Replace positional idx selector dependencies with stable attributes where possible.",
            UiPathFixSuggestionType.WorkflowRefactor,
            UiPathFixRiskLevel.Medium,
            "Selector contains idx.",
            "Selector uses stable semantic attributes or modern targeting."),
        ["RPA020"] = new ManualFixTemplate(
            "Move hard-coded path to configuration",
            "Replace the absolute path literal with a configuration value resolved at runtime.",
            UiPathFixSuggestionType.ConfigurationChange,
            UiPathFixRiskLevel.Low,
            "Property contains an environment-specific absolute path.",
            "Property reads the path from Config or another secure environment configuration source."),
        ["RPA021"] = new ManualFixTemplate(
            "Move email recipient to configuration",
            "Replace the literal recipient with a configured value when it varies by environment or business context.",
            UiPathFixSuggestionType.ConfigurationChange,
            UiPathFixRiskLevel.Low,
            "Mail activity contains a literal recipient.",
            "Mail activity reads the recipient from configuration."),
        ["RPA025"] = new ManualFixTemplate(
            "Split large workflow",
            "Refactor the workflow into smaller reusable workflows with focused responsibilities.",
            UiPathFixSuggestionType.WorkflowRefactor,
            UiPathFixRiskLevel.Medium,
            "Workflow has high activity count or deep nesting.",
            "Workflow is split into focused child workflows with clear inputs and outputs.")
    };

    public override IReadOnlyCollection<string> SupportedRuleIds { get; } = Templates.Keys.ToArray();

    public override UiPathFixSuggestion? Suggest(UiPathFixContext context)
    {
        if (!Templates.TryGetValue(context.Finding.RuleId, out var template))
        {
            return null;
        }

        if (context.Finding.RuleId.Equals("RPA025", StringComparison.OrdinalIgnoreCase))
        {
            return BuildWorkflowRefactoringSuggestion(context, template);
        }

        return new UiPathFixSuggestion
        {
            Id = SuggestionId(context),
            RuleId = context.Finding.RuleId,
            Title = template.Title,
            Description = template.Description,
            FixType = template.FixType,
            Fixability = UiPathFixability.Advisory,
            Confidence = UiPathFixConfidence.Medium,
            RiskLevel = template.RiskLevel,
            WorkflowPath = context.Finding.WorkflowPath,
            ActivityId = context.Finding.ActivityId,
            ActivityName = context.Finding.ActivityName,
            ActivityDisplayName = context.Finding.ActivityDisplayName,
            PropertyName = context.Finding.PropertyName,
            CurrentValue = context.Finding.CurrentValue,
            BeforePreview = template.BeforePreview,
            AfterPreview = template.AfterPreview,
            PatchPreview = TextPreview(template.BeforePreview, template.AfterPreview),
            Explanation = context.Finding.Recommendation ?? template.Description,
            Steps = ["Review the finding in UiPath Studio.", "Make the smallest safe manual change.", "Re-run analysis after the change."],
            Risks = ["This recommendation may affect workflow behavior and should be reviewed manually."],
            ValidationNotes =
            [
                "Review the affected workflow in UiPath Studio.",
                "Re-run analysis after making the manual change."
            ],
            RequiresUserInput = true,
            UserInputHints = ["Confirm the intended behavior before editing the workflow."],
            RequiresAi = false,
            CanAutoApply = false
        };
    }

    private static UiPathFixSuggestion BuildWorkflowRefactoringSuggestion(UiPathFixContext context, ManualFixTemplate template)
    {
        var complexity = context.Workflow?.Analysis?.Complexity;
        var metrics = complexity is null
            ? "The workflow exceeded the configured size or depth threshold."
            : $"Activities: {complexity.ExecutableActivities}; depth: {complexity.MaxNestingDepth}; decisions: {complexity.DecisionCount}; loops: {complexity.LoopCount}; arguments: {complexity.ArgumentCount}.";

        var steps = new List<string>
        {
            "Identify cohesive activity groups with a single business responsibility.",
            "Define explicit In, Out, and InOut arguments before extracting each child workflow.",
            "Extract one group at a time and replace it with Invoke Workflow File.",
            "Run the original and refactored workflows with the same inputs, then re-run analysis."
        };

        if (complexity is { MaxNestingDepth: > 12 })
        {
            steps.Insert(1, "Prioritize the deepest nested branch and preserve its Try Catch and retry boundaries.");
        }

        if (complexity is { DecisionCount: > 10 })
        {
            steps.Insert(1, "Separate decision-heavy business rules from orchestration where their inputs and outputs are explicit.");
        }

        return new UiPathFixSuggestion
        {
            Id = SuggestionId(context),
            RuleId = context.Finding.RuleId,
            Title = template.Title,
            Description = template.Description,
            FixType = template.FixType,
            Fixability = UiPathFixability.Advisory,
            Confidence = complexity is null ? UiPathFixConfidence.Medium : UiPathFixConfidence.High,
            RiskLevel = template.RiskLevel,
            WorkflowPath = context.Finding.WorkflowPath,
            CurrentValue = metrics,
            BeforePreview = metrics,
            AfterPreview = "Focused child workflows connected with explicit arguments and Invoke Workflow File activities.",
            PatchPreview = TextPreview(metrics, "Decomposition plan: cohesive child workflows, explicit contracts, preserved exception/retry boundaries."),
            Explanation = "The recommendation is derived from parsed workflow metrics; it does not invent business boundaries or modify XAML.",
            Steps = steps,
            Risks =
            [
                "Incorrect extraction boundaries can change variable scope, exception propagation, or transaction behavior.",
                "Dynamic workflow references and shared state require manual verification."
            ],
            ValidationNotes =
            [
                "Review proposed boundaries in UiPath Studio.",
                "Verify argument direction and type for each extracted workflow.",
                "Run regression tests and re-run static analysis after each extraction."
            ],
            RequiresUserInput = true,
            UserInputHints = ["Select business-responsibility boundaries; the analyzer intentionally does not infer them automatically."],
            RequiresAi = false,
            CanAutoApply = false
        };
    }

    private sealed record ManualFixTemplate(
        string Title,
        string Description,
        UiPathFixSuggestionType FixType,
        UiPathFixRiskLevel RiskLevel,
        string BeforePreview,
        string AfterPreview);
}

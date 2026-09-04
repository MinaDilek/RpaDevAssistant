using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.Fixes.Providers;

public sealed class DisplayNameFixSuggestionProvider : UiPathFixSuggestionProviderBase
{
    public override IReadOnlyCollection<string> SupportedRuleIds { get; } = ["RPA007"];

    public override UiPathFixSuggestion? Suggest(UiPathFixContext context)
    {
        if (context.Finding.Scope == UiPathFindingScope.Aggregated && context.Activity is null)
        {
            return SuggestAggregateDisplayNameFix(context);
        }

        var activity = context.Activity;
        var activityName = context.Finding.ActivityName ?? activity?.Name;
        var current = context.Finding.ActivityDisplayName ?? activity?.DisplayName;
        if (string.IsNullOrWhiteSpace(activityName) || string.IsNullOrWhiteSpace(current))
        {
            return null;
        }

        var target = GetTargetName(activity);
        var suggested = SuggestDisplayName(activityName, target);
        return new UiPathFixSuggestion
        {
            Id = SuggestionId(context),
            RuleId = context.Finding.RuleId,
            Title = "Use a descriptive activity display name",
            Description = "Rename the generic activity display name to describe intent.",
            FixType = UiPathFixSuggestionType.NamingChange,
            Fixability = UiPathFixability.SafeAutomatic,
            Confidence = target is null ? UiPathFixConfidence.Medium : UiPathFixConfidence.High,
            RiskLevel = UiPathFixRiskLevel.Low,
            WorkflowPath = context.Finding.WorkflowPath,
            ActivityId = context.Finding.ActivityId ?? activity?.ActivityId,
            ActivityName = activityName,
            ActivityDisplayName = current,
            PropertyName = "DisplayName",
            CurrentValue = current,
            SuggestedValue = suggested,
            BeforePreview = $"DisplayName = \"{current}\"",
            AfterPreview = $"DisplayName = \"{suggested}\"",
            PatchPreview = PropertyPreview("DisplayName", current, suggested),
            Explanation = "Descriptive display names make UiPath workflows easier to review, debug, and maintain.",
            Steps = ["Review the proposed DisplayName.", "Apply the DisplayName change in UiPath Studio or through the Safe Apply flow.", "Re-run analysis to confirm the finding is resolved."],
            Risks = ["Low risk: DisplayName is designer metadata and should not affect runtime behavior."],
            ValidationNotes = ["Review the suggested name in context before changing it in UiPath Studio."],
            CanAutoApply = true,
            ExpectedFileHash = TryHashWorkflow(context.Workflow?.FullPath)
        };
    }

    private static UiPathFixSuggestion? SuggestAggregateDisplayNameFix(UiPathFixContext context)
    {
        var affected = context.Finding.AffectedActivities;
        if (affected.Count == 0)
        {
            return null;
        }

        var examples = affected.Take(5).ToArray();
        var before = string.Join(Environment.NewLine, examples.Select(activity =>
            $"{activity.ActivityName}: DisplayName = \"{activity.ActivityDisplayName}\""));
        var after = string.Join(Environment.NewLine, examples.Select(activity =>
            $"{activity.ActivityName}: DisplayName = \"{SuggestDisplayName(activity.ActivityName, null)}\""));

        return new UiPathFixSuggestion
        {
            Id = SuggestionId(context),
            RuleId = context.Finding.RuleId,
            Title = "Rename generic activity display names in this workflow",
            Description = $"{context.Finding.AffectedActivityCount ?? affected.Count} generic DisplayName values were detected in this workflow.",
            FixType = UiPathFixSuggestionType.NamingChange,
            Fixability = UiPathFixability.Previewable,
            Confidence = UiPathFixConfidence.Medium,
            RiskLevel = UiPathFixRiskLevel.Low,
            WorkflowPath = context.Finding.WorkflowPath,
            PropertyName = "DisplayName",
            CurrentValue = $"{context.Finding.AffectedActivityCount ?? affected.Count} generic DisplayName values",
            SuggestedValue = "Rename each affected activity to describe its business intent.",
            BeforePreview = before,
            AfterPreview = after,
            PatchPreview = TextPreview(before, after),
            Explanation = "This is an aggregated workflow-level finding. Review the affected activity list and rename each generic DisplayName to describe what the activity does in its local context.",
            Steps =
            [
                "Open the workflow in UiPath Studio.",
                "Review the affected activities shown in the finding detail.",
                "Rename each generic DisplayName using business intent, target, or action context.",
                "Re-run analysis to confirm the aggregated finding count decreases."
            ],
            Risks = ["Low risk when performed manually: DisplayName is designer metadata and should not change runtime behavior."],
            ValidationNotes = ["Auto-apply is only available when a single affected activity is selected with a stable locator."],
            CanAutoApply = false,
            ExpectedFileHash = TryHashWorkflow(context.Workflow?.FullPath)
        };
    }

    private static string SuggestDisplayName(string activityName, string? target)
    {
        var targetLabel = ToHumanLabel(target);
        return activityName.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant() switch
        {
            "click" => targetLabel is null ? "Click Target" : $"Click {targetLabel}",
            "typeinto" => targetLabel is null ? "Type Into Field" : $"Enter {targetLabel}",
            "gettext" => targetLabel is null ? "Get Text Value" : $"Get {targetLabel} Text",
            "invokeworkflowfile" => "Invoke Workflow File",
            "logmessage" => "Log Workflow Event",
            "httprequest" => targetLabel is null ? "Send HTTP Request" : $"Send {targetLabel} Request",
            "assign" => targetLabel is null ? "Assign Value" : $"Assign {targetLabel}",
            _ => $"{activityName} Target"
        };
    }

    private static string? GetTargetName(RpaDevAssistant.Core.Models.UiPathActivityInfo? activity)
    {
        if (activity is null)
        {
            return null;
        }

        foreach (var key in new[] { "Target", "Selector", "Element", "To", "Value", "Text", "WorkflowFileName" })
        {
            if (activity.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ToHumanLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value
            .Replace("btn", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("txt", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_", " ", StringComparison.Ordinal)
            .Replace("-", " ", StringComparison.Ordinal)
            .Trim();
        return string.Join(' ', System.Text.RegularExpressions.Regex.Split(cleaned, "(?<!^)(?=[A-Z])")
            .SelectMany(part => part.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .Trim();
    }

    private static string? TryHashWorkflow(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            return UiPathFileHash.Sha256(fullPath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

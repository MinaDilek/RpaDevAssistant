namespace RpaDevAssistant.Core.Analysis;

internal static class UiPathRuleRecommendations
{
    private static readonly IReadOnlyDictionary<string, string> Recommendations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["RPA009"] = "Use UiPath Orchestrator Assets/Credentials or secure configuration instead of storing secrets directly in workflows.",
        ["RPA010"] = "Avoid logging credentials, tokens, passwords or sensitive values.",
        ["RPA011"] = "Use realistic configurable timeouts and state-based retry logic.",
        ["RPA012"] = "Use explicit, realistic timeouts that allow normal UI response variability.",
        ["RPA013"] = "Handle expected exceptions explicitly instead of suppressing failures globally.",
        ["RPA014"] = "Reduce broad error suppression and handle expected exceptions near the source.",
        ["RPA015"] = "Consider using an explicit configurable timeout for important UI interactions.",
        ["RPA016"] = "Prefer modern Use Application/Browser targeting where possible.",
        ["RPA017"] = "Prefer stable attributes over idx when possible.",
        ["RPA018"] = "Prefer stable semantic attributes and wildcards where appropriate.",
        ["RPA019"] = "Simplify selectors and prefer stable anchors/modern targeting.",
        ["RPA020"] = "Move environment-specific paths to configuration.",
        ["RPA021"] = "Move recipients to configuration when they vary by environment or business context.",
        ["RPA022"] = "Set a realistic request timeout and handle timeout failures explicitly.",
        ["RPA023"] = "Wrap HTTP calls in local error handling or document the workflow-level failure strategy.",
        ["RPA024"] = "Consider grouping related data into typed models or reducing workflow responsibility.",
        ["RPA025"] = "Split large workflows into focused reusable components."
    };

    public static string? Get(string ruleId)
    {
        return Recommendations.TryGetValue(ruleId, out var recommendation)
            ? recommendation
            : null;
    }
}

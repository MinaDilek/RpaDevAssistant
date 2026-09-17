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
        ["RPA025"] = "Split large workflows into focused reusable components.",
        ["RPA030"] = "Log the business exception context and update transaction status intentionally, or rethrow when the Workflow should not continue.",
        ["RPA031"] = "Use in_, out_, and io_ prefixes that match the argument direction.",
        ["RPA032"] = "Use descriptive lowerCamelCase variable names.",
        ["RPA033"] = "Confirm the variable is unused, then remove it to simplify the workflow.",
        ["RPA034"] = "Confirm the argument is not part of a required caller contract before removing it.",
        ["RPA035"] = "Move endpoint URLs to configuration or Orchestrator assets.",
        ["RPA036"] = "Refactor workflows to break circular invocation cycles and avoid infinite recursion or stack overflow.",
        ["RPA037"] = "Remove unused workflow files or connect them to the process execution chain.",
        ["RPA038"] = "Align the argument direction with whether the workflow reads, writes, or updates the value.",
        ["RPA039"] = "Use In or Out when the workflow does not require both read and write access.",
        ["RPA040"] = "Declare the argument with a valid InArgument, OutArgument, or InOutArgument type.",
        ["RPA041"] = "Move the variable declaration to the narrowest shared scope that contains all references.",
        ["RPA042"] = "Rename the nested variable or reuse the ancestor declaration to avoid shadowing.",
        ["RPA043"] = "Remove the mapping or update it to an argument declared by the target workflow.",
        ["RPA044"] = "Review the target workflow contract and map required input arguments.",
        ["RPA045"] = "Align the Invoke Workflow File mapping direction with the target workflow argument contract.",
        ["RPA046"] = "Where an observable state exists, replace repeated fixed waits with Retry Scope or Check App State."
    };

    public static string? Get(string ruleId)
    {
        return Recommendations.TryGetValue(ruleId, out var recommendation)
            ? recommendation
            : null;
    }
}

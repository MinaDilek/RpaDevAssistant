using System.Text.RegularExpressions;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis.Rules;

public sealed class HardCodedCredentialLikeValueRule : UiPathAnalysisRuleBase
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "Pwd",
        "Secret",
        "Token",
        "ApiKey",
        "Api_Key",
        "ClientSecret",
        "AccessToken"
    };

    private readonly IUiPathExpressionClassifier expressionClassifier;

    public HardCodedCredentialLikeValueRule(IUiPathExpressionClassifier expressionClassifier)
    {
        this.expressionClassifier = expressionClassifier;
    }

    public override string Id => "RPA009";

    public override string Name => "Hard-Coded Credential-Like Value";

    public override string Description => "Detects credential-like activity properties that contain literal values.";

    public override RuleSeverity Severity => RuleSeverity.Critical;

    public override RuleCategory Category => RuleCategory.Security;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities)
            {
                foreach (var property in UiPathPropertyLookup.AllProperties(activity))
                {
                    if (!IsSensitivePropertyName(property.Key) || !LooksLikeHardCodedSecret(property.Value))
                    {
                        continue;
                    }

                    yield return CreateFinding(
                        "Hard-coded credential-like value detected.",
                        "Use UiPath Orchestrator Assets/Credentials or secure configuration instead of storing secrets directly in workflows.",
                        workflow,
                        activity,
                        property.Key,
                        "[REDACTED]");
                }
            }
        }
    }

    private static bool IsSensitivePropertyName(string name)
    {
        var normalized = name.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return SensitiveNames.Contains(name) || SensitiveNames.Any(
            sensitive => normalized.Equals(
                sensitive.Replace("_", string.Empty, StringComparison.Ordinal),
                StringComparison.OrdinalIgnoreCase));
    }

    private bool LooksLikeHardCodedSecret(string? value)
    {
        if (IsNullMarker(value))
        {
            return false;
        }

        var classification = expressionClassifier.Classify(value);
        if (classification.Kind != UiPathExpressionKind.LiteralString || string.IsNullOrWhiteSpace(classification.LiteralValue))
        {
            return false;
        }

        var literal = classification.LiteralValue.Trim();
        if (literal.Length < 6)
        {
            return false;
        }

        return literal.Any(char.IsDigit) || literal.Any(char.IsPunctuation) || literal.Length >= 12;
    }

    private static bool IsNullMarker(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        return trimmed.Equals("{x:Null}", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Nothing", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("null", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class SensitiveValueInLogMessageRule : UiPathAnalysisRuleBase
{
    private static readonly Regex ReferencedIdentifierRegex = new(
        @"(\+|&|\{|\(|,)\s*(?<identifier>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override string Id => "RPA010";

    public override string Name => "Potential Sensitive Data Logging";

    public override string Description => "Detects Log Message activities that appear to log sensitive values.";

    public override RuleSeverity Severity => RuleSeverity.Error;

    public override RuleCategory Category => RuleCategory.Security;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities.Where(activity => UiPathActivityClassifier.IsNamed(activity, "LogMessage", "Log Message")))
            {
                if (!UiPathPropertyLookup.TryGet(activity, out var message, "Message", "Text") ||
                    string.IsNullOrWhiteSpace(message) ||
                    !LooksLikeSensitiveValueLogging(message))
                {
                    continue;
                }

                yield return CreateFinding(
                    "Log Message may expose sensitive data.",
                    "Avoid logging credentials, tokens, passwords or sensitive values.",
                    workflow,
                    activity,
                    "Message",
                    "[REDACTED]");
            }
        }
    }

    private static bool LooksLikeSensitiveValueLogging(string message)
    {
        return ReferencedIdentifierRegex.Matches(message)
            .Select(match => match.Groups["identifier"].Value)
            .Any(identifier =>
                identifier.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                identifier.Contains("pwd", StringComparison.OrdinalIgnoreCase) ||
                identifier.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                identifier.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                identifier.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
                identifier.Contains("apikey", StringComparison.OrdinalIgnoreCase) ||
                identifier.Contains("api_key", StringComparison.OrdinalIgnoreCase) ||
                identifier.Contains("authorization", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class ExcessiveUiTimeoutRule : TimeoutRuleBase
{
    public ExcessiveUiTimeoutRule(IUiPathExpressionClassifier expressionClassifier)
        : base(expressionClassifier)
    {
    }

    public override string Id => "RPA011";

    public override string Name => "Excessive UI Timeout";

    public override string Description => "Detects hard-coded UI automation timeouts that are unusually high.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override RuleCategory Category => RuleCategory.Performance;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var candidate in FindUiTimeouts(context))
        {
            if (candidate.TimeoutMs < UiPathAnalysisThresholds.Default.ExcessiveTimeoutMs)
            {
                continue;
            }

            yield return CreateFinding(
                "UI automation timeout is excessively high.",
                "Use realistic configurable timeouts and state-based retry logic.",
                candidate.Workflow,
                candidate.Activity,
                candidate.PropertyName,
                candidate.CurrentValue);
        }
    }
}

public sealed class UnrealisticallyLowUiTimeoutRule : TimeoutRuleBase
{
    public UnrealisticallyLowUiTimeoutRule(IUiPathExpressionClassifier expressionClassifier)
        : base(expressionClassifier)
    {
    }

    public override string Id => "RPA012";

    public override string Name => "Unrealistically Low UI Timeout";

    public override string Description => "Detects hard-coded UI automation timeouts that are too low to be reliable.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override RuleCategory Category => RuleCategory.Reliability;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var candidate in FindUiTimeouts(context))
        {
            if (candidate.TimeoutMs <= 0 || candidate.TimeoutMs >= UiPathAnalysisThresholds.Default.LowTimeoutMs)
            {
                continue;
            }

            yield return CreateFinding(
                "UI automation timeout is unrealistically low.",
                "Use explicit, realistic timeouts that allow normal UI response variability.",
                candidate.Workflow,
                candidate.Activity,
                candidate.PropertyName,
                candidate.CurrentValue);
        }
    }
}

public sealed class ContinueOnErrorEnabledRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA013";

    public override string Name => "ContinueOnError Enabled";

    public override string Description => "Detects executable activities that suppress failures with ContinueOnError.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override RuleCategory Category => RuleCategory.ExceptionHandling;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities.Where(UiPathActivityClassifier.IsExecutable))
            {
                if (!UiPathWorkflowMetricsCalculator.HasContinueOnErrorEnabled(activity))
                {
                    continue;
                }

                yield return CreateFinding(
                    "ContinueOnError is enabled on an executable activity.",
                    "Handle expected exceptions explicitly instead of suppressing failures globally.",
                    workflow,
                    activity,
                    "ContinueOnError",
                    "True");
            }
        }
    }
}

public sealed class ExcessiveContinueOnErrorUsageRule : UiPathAnalysisRuleBase
{
    private readonly IUiPathWorkflowMetricsCalculator metricsCalculator;

    public ExcessiveContinueOnErrorUsageRule(IUiPathWorkflowMetricsCalculator metricsCalculator)
    {
        this.metricsCalculator = metricsCalculator;
    }

    public override string Id => "RPA014";

    public override string Name => "Excessive ContinueOnError Usage";

    public override string Description => "Detects workflows where ContinueOnError is used repeatedly.";

    public override RuleSeverity Severity => RuleSeverity.Error;

    public override RuleCategory Category => RuleCategory.Reliability;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            var metrics = metricsCalculator.Calculate(workflow);
            if (metrics.ContinueOnErrorCount < UiPathAnalysisThresholds.Default.ContinueOnErrorWorkflowThreshold)
            {
                continue;
            }

            yield return CreateFinding(
                $"Workflow uses ContinueOnError {metrics.ContinueOnErrorCount} times.",
                "Reduce broad error suppression and handle expected exceptions near the source.",
                workflow,
                currentValue: metrics.ContinueOnErrorCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}

public sealed class MissingExplicitTimeoutOnCriticalUiActivityRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA015";

    public override string Name => "Missing Explicit Timeout on Critical UI Activity";

    public override string Description => "Detects legacy UI automation activities with selectors and no explicit timeout.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Reliability;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities.Where(IsConservativeUiCandidate))
            {
                if (UiPathPropertyLookup.TryGet(activity, out _, "TimeoutMS", "Timeout") ||
                    !UiPathPropertyLookup.TryGet(activity, out var selector, "Selector", "Target") ||
                    string.IsNullOrWhiteSpace(selector))
                {
                    continue;
                }

                yield return CreateFinding(
                    "Critical UI activity does not define an explicit timeout.",
                    "Consider using an explicit configurable timeout for important UI interactions.",
                    workflow,
                    activity,
                    "TimeoutMS");
            }
        }
    }

    private static bool IsConservativeUiCandidate(UiPathActivityInfo activity)
    {
        return UiPathActivityClassifier.IsNamed(activity, "Click", "TypeInto", "Type Into", "GetText", "Get Text");
    }
}

public sealed class LegacyUiAutomationActivityRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA016";

    public override string Name => "Legacy UI Automation Activity";

    public override string Description => "Detects legacy UI automation activities in modern or Windows-compatible projects.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.UiAutomation;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        var compatibility = context.Project.Compatibility ?? string.Empty;
        var modernProject = compatibility.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
            compatibility.Contains("Modern", StringComparison.OrdinalIgnoreCase);
        if (!modernProject)
        {
            yield break;
        }

        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities.Where(IsLegacyActivity))
            {
                yield return CreateFinding(
                    "Legacy UI automation activity detected.",
                    "Prefer modern Use Application/Browser targeting where possible.",
                    workflow,
                    activity);
            }
        }
    }

    private static bool IsLegacyActivity(UiPathActivityInfo activity)
    {
        return UiPathActivityClassifier.IsNamed(activity, "OpenBrowser", "Open Browser", "AttachBrowser", "Attach Browser", "ElementExists", "Element Exists");
    }
}

public sealed class SelectorUsesIdxAttributeRule : SelectorRuleBase
{
    public SelectorUsesIdxAttributeRule(IUiPathSelectorAnalyzer selectorAnalyzer)
        : base(selectorAnalyzer)
    {
    }

    public override string Id => "RPA017";

    public override string Name => "Selector Uses idx Attribute";

    public override string Description => "Detects selectors that depend on positional idx attributes.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override RuleCategory Category => RuleCategory.UiAutomation;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var candidate in FindSelectors(context))
        {
            if (!candidate.Analysis.ContainsIdx)
            {
                continue;
            }

            yield return CreateFinding(
                "Selector uses an idx attribute.",
                "Prefer stable attributes over idx when possible.",
                candidate.Workflow,
                candidate.Activity,
                candidate.PropertyName,
                "idx");
        }
    }
}

public sealed class PotentiallyUnstableSelectorAttributeRule : SelectorRuleBase
{
    public PotentiallyUnstableSelectorAttributeRule(IUiPathSelectorAnalyzer selectorAnalyzer)
        : base(selectorAnalyzer)
    {
    }

    public override string Id => "RPA018";

    public override string Name => "Potentially Unstable Selector Attribute";

    public override string Description => "Detects selectors that appear to depend on generated numeric or GUID identifiers.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override RuleCategory Category => RuleCategory.UiAutomation;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var candidate in FindSelectors(context))
        {
            if (candidate.Analysis.PotentialDynamicAttributes.Count == 0)
            {
                continue;
            }

            yield return CreateFinding(
                "Selector may contain unstable generated attributes.",
                "Prefer stable semantic attributes and wildcards where appropriate.",
                candidate.Workflow,
                candidate.Activity,
                candidate.PropertyName,
                string.Join(", ", candidate.Analysis.PotentialDynamicAttributes));
        }
    }
}

public sealed class OverlyComplexSelectorRule : SelectorRuleBase
{
    public OverlyComplexSelectorRule(IUiPathSelectorAnalyzer selectorAnalyzer)
        : base(selectorAnalyzer)
    {
    }

    public override string Id => "RPA019";

    public override string Name => "Overly Complex Selector";

    public override string Description => "Detects selector literals that are very long and difficult to maintain.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Maintainability;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var candidate in FindSelectors(context))
        {
            if (candidate.Analysis.Length <= UiPathAnalysisThresholds.Default.SelectorLengthThreshold)
            {
                continue;
            }

            yield return CreateFinding(
                $"Selector is {candidate.Analysis.Length} characters long.",
                "Simplify selectors and prefer stable anchors/modern targeting.",
                candidate.Workflow,
                candidate.Activity,
                candidate.PropertyName,
                candidate.Analysis.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}

public sealed class HardCodedAbsoluteFilePathRule : UiPathAnalysisRuleBase
{
    private static readonly Regex WindowsPathRegex = new(
        @"(?:^|[""'])(([A-Za-z]:\\|\\\\)[^""']+)",
        RegexOptions.Compiled);

    private readonly IUiPathExpressionClassifier expressionClassifier;

    public HardCodedAbsoluteFilePathRule(IUiPathExpressionClassifier expressionClassifier)
    {
        this.expressionClassifier = expressionClassifier;
    }

    public override string Id => "RPA020";

    public override string Name => "Hard-Coded Absolute File Path";

    public override string Description => "Detects hard-coded absolute local or UNC paths in workflow properties.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override RuleCategory Category => RuleCategory.Configuration;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities)
            {
                foreach (var property in UiPathPropertyLookup.AllProperties(activity))
                {
                    if (!TryFindHardCodedPath(property.Value, out var path))
                    {
                        continue;
                    }

                    yield return CreateFinding(
                        "Hard-coded absolute file path detected.",
                        "Move environment-specific paths to configuration.",
                        workflow,
                        activity,
                        property.Key,
                        path);
                }
            }
        }
    }

    private bool TryFindHardCodedPath(string? value, out string? path)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var classification = expressionClassifier.Classify(value);
        if (classification.Kind is UiPathExpressionKind.ConfigReference or UiPathExpressionKind.VariableReference)
        {
            return false;
        }

        var unwrapped = UiPathExpressionClassifier.UnwrapExpression(value.Trim());
        if (unwrapped.Contains("Path.Combine", StringComparison.OrdinalIgnoreCase) ||
            unwrapped.Contains("Environment.", StringComparison.OrdinalIgnoreCase) ||
            unwrapped.Contains("Config(", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var match = WindowsPathRegex.Match(unwrapped);
        if (!match.Success)
        {
            return false;
        }

        path = match.Groups[1].Value;
        return true;
    }
}

public sealed class HardCodedEmailAddressRule : UiPathAnalysisRuleBase
{
    private static readonly Regex EmailRegex = new(
        @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IUiPathExpressionClassifier expressionClassifier;

    public HardCodedEmailAddressRule(IUiPathExpressionClassifier expressionClassifier)
    {
        this.expressionClassifier = expressionClassifier;
    }

    public override string Id => "RPA021";

    public override string Name => "Hard-Coded Email Address";

    public override string Description => "Detects literal email recipients in mail-related activities.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Configuration;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities.Where(IsMailActivity))
            {
                foreach (var propertyName in new[] { "To", "Cc", "Bcc", "From", "ReplyTo", "Recipients" })
                {
                    if (!UiPathPropertyLookup.TryGet(activity, out var value, propertyName) ||
                        !IsLiteralEmail(value, out var email))
                    {
                        continue;
                    }

                    yield return CreateFinding(
                        "Mail activity contains a hard-coded email address.",
                        "Move recipients to configuration when they vary by environment or business context.",
                        workflow,
                        activity,
                        propertyName,
                        email);
                }
            }
        }
    }

    private bool IsLiteralEmail(string? value, out string? email)
    {
        email = null;
        var classification = expressionClassifier.Classify(value);
        if (classification.Kind is UiPathExpressionKind.ConfigReference or UiPathExpressionKind.VariableReference)
        {
            return false;
        }

        var literal = classification.LiteralValue ?? value ?? string.Empty;
        var match = EmailRegex.Match(literal);
        if (!match.Success)
        {
            return false;
        }

        email = match.Value;
        return true;
    }

    private static bool IsMailActivity(UiPathActivityInfo activity)
    {
        return activity.Name.Contains("Mail", StringComparison.OrdinalIgnoreCase) ||
            activity.Name.Contains("SMTP", StringComparison.OrdinalIgnoreCase) ||
            activity.Name.Contains("Outlook", StringComparison.OrdinalIgnoreCase) ||
            activity.Namespace?.Contains("Mail", StringComparison.OrdinalIgnoreCase) == true;
    }
}

public sealed class HttpRequestWithoutExplicitTimeoutRule : UiPathAnalysisRuleBase
{
    private readonly IUiPathExpressionClassifier expressionClassifier;

    public HttpRequestWithoutExplicitTimeoutRule(IUiPathExpressionClassifier expressionClassifier)
    {
        this.expressionClassifier = expressionClassifier;
    }

    public override string Id => "RPA022";

    public override string Name => "HTTP Request Without Explicit Timeout";

    public override string Description => "Detects HTTP Request activities without a valid explicit timeout.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override RuleCategory Category => RuleCategory.Reliability;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities.Where(IsHttpRequest))
            {
                if (!UiPathPropertyLookup.TryGet(activity, out var timeout, "Timeout", "TimeoutMS", "RequestTimeout"))
                {
                    yield return CreateFinding(
                        "HTTP Request does not define a valid explicit timeout.",
                        "Set a realistic request timeout and handle timeout failures explicitly.",
                        workflow,
                        activity,
                        "Timeout",
                        timeout);
                }

                var classification = expressionClassifier.Classify(timeout);
                if (!classification.IsHardCodedLiteral)
                {
                    continue;
                }

                if (!expressionClassifier.TryParseTimeoutMilliseconds(timeout, out var milliseconds) || milliseconds <= 0)
                {
                    yield return CreateFinding(
                        "HTTP Request does not define a valid explicit timeout.",
                        "Set a realistic request timeout and handle timeout failures explicitly.",
                        workflow,
                        activity,
                        "Timeout",
                        timeout);
                }
            }
        }
    }

    private static bool IsHttpRequest(UiPathActivityInfo activity)
    {
        return UiPathActivityClassifier.IsNamed(activity, "HTTPRequest", "HTTP Request", "HttpRequest");
    }
}

public sealed class HttpRequestWithoutLocalErrorHandlingRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA023";

    public override string Name => "HTTP Request Without Local Error Handling";

    public override string Description => "Detects HTTP Request activities that are not inside a local TryCatch.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override RuleCategory Category => RuleCategory.ExceptionHandling;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            var activitiesById = workflow.Activities.ToDictionary(
                activity => activity.ActivityId,
                StringComparer.OrdinalIgnoreCase);

            foreach (var activity in workflow.Activities.Where(activity => UiPathActivityClassifier.IsNamed(activity, "HTTPRequest", "HTTP Request", "HttpRequest")))
            {
                if (HasAncestor(activity, activitiesById, "TryCatch"))
                {
                    continue;
                }

                yield return CreateFinding(
                    "HTTP Request is not protected by a local TryCatch.",
                    "Wrap HTTP calls in local error handling or document the workflow-level failure strategy.",
                    workflow,
                    activity);
            }
        }
    }

    private static bool HasAncestor(UiPathActivityInfo activity, IReadOnlyDictionary<string, UiPathActivityInfo> activitiesById, string ancestorName)
    {
        var parentId = activity.ParentActivityId;
        while (!string.IsNullOrWhiteSpace(parentId) && activitiesById.TryGetValue(parentId, out var parent))
        {
            if (UiPathActivityClassifier.IsNamed(parent, ancestorName))
            {
                return true;
            }

            parentId = parent.ParentActivityId;
        }

        return false;
    }
}

public sealed class ExcessiveWorkflowArgumentsRule : UiPathAnalysisRuleBase
{
    private readonly IUiPathWorkflowMetricsCalculator metricsCalculator;

    public ExcessiveWorkflowArgumentsRule(IUiPathWorkflowMetricsCalculator metricsCalculator)
    {
        this.metricsCalculator = metricsCalculator;
    }

    public override string Id => "RPA024";

    public override string Name => "Excessive Workflow Arguments";

    public override string Description => "Detects workflows with too many arguments.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            var metrics = metricsCalculator.Calculate(workflow);
            if (metrics.ArgumentCount <= UiPathAnalysisThresholds.Default.WorkflowArgumentThreshold)
            {
                continue;
            }

            yield return CreateFinding(
                $"Workflow defines {metrics.ArgumentCount} arguments.",
                "Consider grouping related data into typed models or reducing workflow responsibility.",
                workflow,
                currentValue: metrics.ArgumentCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}

public sealed class LargeWorkflowRule : UiPathAnalysisRuleBase
{
    private readonly IUiPathWorkflowMetricsCalculator metricsCalculator;

    public LargeWorkflowRule(IUiPathWorkflowMetricsCalculator metricsCalculator)
    {
        this.metricsCalculator = metricsCalculator;
    }

    public override string Id => "RPA025";

    public override string Name => "Large Workflow";

    public override string Description => "Detects workflows that are large or deeply nested enough to be difficult to maintain.";

    public override RuleSeverity Severity => RuleSeverity.Warning;

    public override RuleCategory Category => RuleCategory.Maintainability;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            var metrics = metricsCalculator.Calculate(workflow);
            var activityExceeded = metrics.ExecutableActivityCount > UiPathAnalysisThresholds.Default.LargeWorkflowActivityThreshold;
            var depthExceeded = metrics.MaxDepth > UiPathAnalysisThresholds.Default.LargeWorkflowDepthThreshold;
            var complexityExceeded = metrics.ComplexityLevel is UiPathWorkflowComplexityLevel.High or UiPathWorkflowComplexityLevel.VeryHigh;
            if (!activityExceeded && !depthExceeded && !complexityExceeded)
            {
                continue;
            }

            var reasons = new List<string>();
            if (activityExceeded)
            {
                reasons.Add($"{metrics.ExecutableActivityCount} executable activities");
            }

            if (depthExceeded)
            {
                reasons.Add($"depth {metrics.MaxDepth}");
            }

            if (complexityExceeded)
            {
                reasons.Add($"complexity score {metrics.ComplexityScore} ({metrics.ComplexityLevel})");
            }

            var message = $"Workflow is complex: {string.Join(", ", reasons)}.";

            yield return CreateFinding(
                message,
                "Split large workflows into focused reusable components.",
                workflow,
                currentValue: $"activities={metrics.ExecutableActivityCount}; depth={metrics.MaxDepth}; decisions={metrics.DecisionCount}; loops={metrics.LoopCount}; complexity={metrics.ComplexityScore}");
        }
    }
}

public abstract class TimeoutRuleBase : UiPathAnalysisRuleBase
{
    private readonly IUiPathExpressionClassifier expressionClassifier;

    protected TimeoutRuleBase(IUiPathExpressionClassifier expressionClassifier)
    {
        this.expressionClassifier = expressionClassifier;
    }

    protected IEnumerable<TimeoutCandidate> FindUiTimeouts(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities.Where(UiPathWorkflowMetricsCalculator.IsUiActivity))
            {
                if (!UiPathPropertyLookup.TryGet(activity, out var timeout, "TimeoutMS", "Timeout", "RequestTimeout") ||
                    !expressionClassifier.TryParseTimeoutMilliseconds(timeout, out var milliseconds))
                {
                    continue;
                }

                yield return new TimeoutCandidate(workflow, activity, "TimeoutMS", timeout, milliseconds);
            }
        }
    }

    protected sealed record TimeoutCandidate(
        UiPathWorkflowAnalysis Workflow,
        UiPathActivityInfo Activity,
        string PropertyName,
        string? CurrentValue,
        int TimeoutMs);
}

public abstract class SelectorRuleBase : UiPathAnalysisRuleBase
{
    private readonly IUiPathSelectorAnalyzer selectorAnalyzer;

    protected SelectorRuleBase(IUiPathSelectorAnalyzer selectorAnalyzer)
    {
        this.selectorAnalyzer = selectorAnalyzer;
    }

    protected IEnumerable<SelectorCandidate> FindSelectors(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities)
            {
                foreach (var propertyName in new[] { "Selector", "Target", "Target.Selector" })
                {
                    if (!UiPathPropertyLookup.TryGet(activity, out var selector, propertyName) ||
                        string.IsNullOrWhiteSpace(selector))
                    {
                        continue;
                    }

                    yield return new SelectorCandidate(
                        workflow,
                        activity,
                        propertyName,
                        selectorAnalyzer.Analyze(selector));
                }
            }
        }
    }

    protected sealed record SelectorCandidate(
        UiPathWorkflowAnalysis Workflow,
        UiPathActivityInfo Activity,
        string PropertyName,
        UiPathSelectorAnalysis Analysis);
}

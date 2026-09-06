using System.Globalization;
using RpaDevAssistant.Core.Analysis.RuleCatalog;
using RpaDevAssistant.Core.Dependencies;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis.CustomRules;

public sealed class UiPathCustomRuleEvaluator : IUiPathCustomRuleEvaluator
{
    private readonly UiPathCustomRuleOptions options;

    public UiPathCustomRuleEvaluator(UiPathCustomRuleOptions? options = null)
    {
        this.options = options ?? new UiPathCustomRuleOptions();
    }

    public IReadOnlyList<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context, IEnumerable<UiPathCustomRuleDefinition> rules)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rules);

        return rules
            .Where(rule => rule.Enabled)
            .SelectMany(rule => EvaluateRule(context, rule))
            .ToArray();
    }

    public UiPathCustomRuleTestResult Test(UiPathAnalysisContext context, UiPathCustomRuleDefinition rule)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rule);

        var findings = EvaluateRule(context, rule with { Enabled = true }).ToArray();
        var workflows = findings
            .Select(finding => finding.WorkflowPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();
        var activities = findings
            .Where(finding => finding.ActivityId is not null)
            .Select(finding => new UiPathAffectedActivity
            {
                ActivityId = finding.ActivityId!,
                ActivityName = finding.ActivityName ?? "Activity",
                ActivityDisplayName = finding.ActivityDisplayName ?? finding.ActivityName ?? "Activity",
                ActivityPath = null,
                PropertyName = finding.PropertyName,
                CurrentValue = finding.CurrentValue
            })
            .Take(50)
            .ToArray();

        var estimated = findings.Count();
        return new UiPathCustomRuleTestResult
        {
            RuleId = rule.Id,
            MatchedWorkflowCount = workflows.Length,
            MatchedActivityCount = findings.Count(finding => finding.ActivityId is not null),
            EstimatedFindingCount = estimated,
            HasNoiseWarning = estimated >= options.NoiseWarningThreshold,
            NoiseWarning = estimated >= options.NoiseWarningThreshold
                ? $"This rule matches {estimated:N0} items and may create excessive noise."
                : null,
            MatchedWorkflows = workflows,
            MatchedActivities = activities
        };
    }

    private static IEnumerable<UiPathAnalysisFinding> EvaluateRule(UiPathAnalysisContext context, UiPathCustomRuleDefinition rule)
    {
        return rule.Scope switch
        {
            UiPathRuleScope.Activity => EvaluateActivityRule(context, rule),
            UiPathRuleScope.Workflow => EvaluateWorkflowRule(context, rule),
            UiPathRuleScope.Project => EvaluateProjectRule(context, rule),
            _ => []
        };
    }

    private static IEnumerable<UiPathAnalysisFinding> EvaluateActivityRule(UiPathAnalysisContext context, UiPathCustomRuleDefinition rule)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities)
            {
                if (Matches(rule, condition => ResolveValue(context.Project, workflow, activity, condition)))
                {
                    yield return Finding(rule, workflow, activity);
                }
            }
        }
    }

    private static IEnumerable<UiPathAnalysisFinding> EvaluateWorkflowRule(UiPathAnalysisContext context, UiPathCustomRuleDefinition rule)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            if (Matches(rule, condition => ResolveValue(context.Project, workflow, null, condition)))
            {
                yield return Finding(rule, workflow, null);
            }
        }
    }

    private static IEnumerable<UiPathAnalysisFinding> EvaluateProjectRule(UiPathAnalysisContext context, UiPathCustomRuleDefinition rule)
    {
        if (rule.Conditions.Any(condition => condition.Field.StartsWith("Dependency.", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var dependency in context.Project.DependencyAnalysis?.Packages ?? [])
            {
                if (Matches(rule, condition => ResolveDependencyValue(dependency, condition)))
                {
                    yield return Finding(rule, dependency);
                }
            }

            yield break;
        }

        if (Matches(rule, condition => ResolveValue(context.Project, null, null, condition)))
        {
            yield return Finding(rule, null, null);
        }
    }

    private static bool Matches(UiPathCustomRuleDefinition rule, Func<UiPathRuleCondition, object?> resolve)
    {
        return rule.MatchMode == UiPathCustomRuleMatchMode.All
            ? rule.Conditions.All(condition => MatchesCondition(resolve(condition), condition))
            : rule.Conditions.Any(condition => MatchesCondition(resolve(condition), condition));
    }

    private static object? ResolveValue(ProjectScanResult project, UiPathWorkflowAnalysis? workflow, UiPathActivityInfo? activity, UiPathRuleCondition condition)
    {
        return condition.Field switch
        {
            "Activity.Name" => activity?.Name,
            "Activity.DisplayName" => activity?.DisplayName,
            "Activity.Property" => ResolveActivityProperty(activity, ResolvePropertyCondition(condition).PropertyName),
            "Workflow.Name" => workflow?.FileName,
            "Workflow.Path" => workflow?.RelativePath,
            "Workflow.ActivityCount" => workflow?.ActivityCount,
            "Workflow.ExecutableActivityCount" => workflow?.Complexity?.ExecutableActivities,
            "Workflow.MaxNestingDepth" => workflow?.Complexity?.MaxNestingDepth,
            "Project.Compatibility" => project.Compatibility,
            "Project.IsReFramework" => project.IsReFramework,
            "Dependency.Name" => project.Dependencies.Select(dependency => dependency.Name).ToArray(),
            "Dependency.Version" => project.Dependencies.Select(dependency => dependency.Version).Where(version => !string.IsNullOrWhiteSpace(version)).ToArray(),
            "Dependency.Category" => project.DependencyAnalysis?.Packages.Select(dependency => dependency.Category.ToString()).ToArray(),
            "Dependency.UsageStatus" => project.DependencyAnalysis?.Packages.Select(dependency => dependency.UsageStatus.ToString()).ToArray(),
            "Dependency.RiskLevel" => project.DependencyAnalysis?.Packages.Select(dependency => dependency.RiskLevel.ToString()).ToArray(),
            _ => null
        };
    }

    private static object? ResolveDependencyValue(UiPathDependencyAnalysis dependency, UiPathRuleCondition condition)
    {
        return condition.Field switch
        {
            "Dependency.Name" => dependency.Name,
            "Dependency.Version" => dependency.DeclaredVersion,
            "Dependency.Category" => dependency.Category.ToString(),
            "Dependency.UsageStatus" => dependency.UsageStatus.ToString(),
            "Dependency.RiskLevel" => dependency.RiskLevel.ToString(),
            _ => null
        };
    }

    private static string? ResolveActivityProperty(UiPathActivityInfo? activity, string? propertyName)
    {
        if (activity is null || string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        return activity.Properties.TryGetValue(propertyName, out var value)
            ? value
            : activity.Properties.FirstOrDefault(property => property.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static bool MatchesCondition(object? actual, UiPathRuleCondition condition)
    {
        var effectiveCondition = condition.Field.Equals("Activity.Property", StringComparison.OrdinalIgnoreCase)
            ? condition with { Value = ResolvePropertyCondition(condition).ExpectedValue }
            : condition;

        if (actual is string[] values)
        {
            return effectiveCondition.Operator switch
            {
                UiPathRuleConditionOperator.Exists => values.Length > 0,
                UiPathRuleConditionOperator.NotExists => values.Length == 0,
                _ => values.Any(value => MatchesCondition(value, effectiveCondition))
            };
        }

        if (effectiveCondition.Operator == UiPathRuleConditionOperator.Exists)
        {
            return actual is not null && !string.IsNullOrWhiteSpace(Convert.ToString(actual, CultureInfo.InvariantCulture));
        }

        if (effectiveCondition.Operator == UiPathRuleConditionOperator.NotExists)
        {
            return actual is null || string.IsNullOrWhiteSpace(Convert.ToString(actual, CultureInfo.InvariantCulture));
        }

        if (actual is int or long or double or decimal)
        {
            return MatchNumber(Convert.ToDouble(actual, CultureInfo.InvariantCulture), effectiveCondition);
        }

        if (actual is bool actualBool && bool.TryParse(effectiveCondition.Value, out var expectedBool))
        {
            return effectiveCondition.Operator switch
            {
                UiPathRuleConditionOperator.Equals => actualBool == expectedBool,
                UiPathRuleConditionOperator.NotEquals => actualBool != expectedBool,
                _ => false
            };
        }

        if (IsNumericComparison(effectiveCondition.Operator)
            && double.TryParse(Convert.ToString(actual, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var actualNumber))
        {
            return MatchNumber(actualNumber, effectiveCondition);
        }

        return MatchString(Convert.ToString(actual, CultureInfo.InvariantCulture), effectiveCondition);
    }

    private static (string? PropertyName, string? ExpectedValue) ResolvePropertyCondition(UiPathRuleCondition condition)
    {
        if (!string.IsNullOrWhiteSpace(condition.PropertyName))
        {
            return (condition.PropertyName, condition.CompareValue ?? condition.Value);
        }

        var value = condition.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, condition.CompareValue);
        }

        var separatorIndex = value.IndexOf('=');
        if (separatorIndex < 0)
        {
            separatorIndex = value.IndexOf(':');
        }

        if (separatorIndex > 0)
        {
            var propertyName = value[..separatorIndex].Trim();
            var expectedValue = value[(separatorIndex + 1)..].Trim();
            return (propertyName, condition.CompareValue ?? expectedValue);
        }

        return (value, condition.CompareValue ?? value);
    }

    private static bool MatchNumber(double actual, UiPathRuleCondition condition)
    {
        if (!double.TryParse(condition.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var expected))
        {
            return false;
        }

        return condition.Operator switch
        {
            UiPathRuleConditionOperator.Equals => Math.Abs(actual - expected) < 0.0001,
            UiPathRuleConditionOperator.NotEquals => Math.Abs(actual - expected) >= 0.0001,
            UiPathRuleConditionOperator.GreaterThan => actual > expected,
            UiPathRuleConditionOperator.GreaterThanOrEqual => actual >= expected,
            UiPathRuleConditionOperator.LessThan => actual < expected,
            UiPathRuleConditionOperator.LessThanOrEqual => actual <= expected,
            _ => false
        };
    }

    private static bool IsNumericComparison(UiPathRuleConditionOperator @operator)
    {
        return @operator is UiPathRuleConditionOperator.GreaterThan
            or UiPathRuleConditionOperator.GreaterThanOrEqual
            or UiPathRuleConditionOperator.LessThan
            or UiPathRuleConditionOperator.LessThanOrEqual;
    }

    private static bool MatchString(string? actual, UiPathRuleCondition condition)
    {
        var comparison = condition.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var expected = condition.Value ?? string.Empty;
        actual ??= string.Empty;

        return condition.Operator switch
        {
            UiPathRuleConditionOperator.Equals => actual.Equals(expected, comparison),
            UiPathRuleConditionOperator.NotEquals => !actual.Equals(expected, comparison),
            UiPathRuleConditionOperator.Contains => actual.Contains(expected, comparison),
            UiPathRuleConditionOperator.StartsWith => actual.StartsWith(expected, comparison),
            UiPathRuleConditionOperator.EndsWith => actual.EndsWith(expected, comparison),
            _ => false
        };
    }

    private static UiPathAnalysisFinding Finding(UiPathCustomRuleDefinition rule, UiPathWorkflowAnalysis? workflow, UiPathActivityInfo? activity)
    {
        var propertyEvidence = ResolveFindingPropertyEvidence(rule, activity);
        return new UiPathAnalysisFinding
        {
            RuleId = rule.Id,
            RuleName = CustomText(rule.Name, rule.NameEn, rule.NameTr),
            Severity = rule.Severity,
            Category = rule.Category,
            Message = CustomText(rule.Description ?? $"Custom rule '{rule.Name}' matched.", rule.DescriptionEn, rule.DescriptionTr),
            Description = CustomText(rule.Description ?? string.Empty, rule.DescriptionEn, rule.DescriptionTr),
            Recommendation = CustomText(rule.Recommendation ?? string.Empty, rule.RecommendationEn, rule.RecommendationTr),
            WorkflowPath = workflow?.RelativePath,
            ActivityId = activity?.ActivityId,
            ActivityName = activity?.Name,
            ActivityDisplayName = activity?.DisplayName,
            PropertyName = propertyEvidence.PropertyName,
            CurrentValue = propertyEvidence.CurrentValue,
            Scope = rule.Scope == UiPathRuleScope.Project
                ? UiPathFindingScope.Project
                : rule.Scope == UiPathRuleScope.Workflow ? UiPathFindingScope.Workflow : UiPathFindingScope.Activity,
            Source = "Custom"
        };
    }

    private static UiPathAnalysisFinding Finding(UiPathCustomRuleDefinition rule, UiPathDependencyAnalysis dependency)
    {
        return new UiPathAnalysisFinding
        {
            RuleId = rule.Id,
            RuleName = CustomText(rule.Name, rule.NameEn, rule.NameTr),
            Severity = rule.Severity,
            Category = rule.Category,
            Message = CustomText(rule.Description ?? $"Custom rule '{rule.Name}' matched.", rule.DescriptionEn, rule.DescriptionTr),
            Description = CustomText(rule.Description ?? string.Empty, rule.DescriptionEn, rule.DescriptionTr),
            Recommendation = CustomText(rule.Recommendation ?? string.Empty, rule.RecommendationEn, rule.RecommendationTr),
            PropertyName = "dependencies",
            CurrentValue = $"{dependency.Name} ({dependency.UsageStatus}, {dependency.RiskLevel})",
            Scope = UiPathFindingScope.Project,
            Source = "Custom"
        };
    }

    private static (string? PropertyName, string? CurrentValue) ResolveFindingPropertyEvidence(UiPathCustomRuleDefinition rule, UiPathActivityInfo? activity)
    {
        if (activity is null)
        {
            return (null, null);
        }

        var propertyCondition = rule.Conditions.FirstOrDefault(condition => condition.Field.Equals("Activity.Property", StringComparison.OrdinalIgnoreCase));
        if (propertyCondition is null)
        {
            return (null, null);
        }

        var propertyName = ResolvePropertyCondition(propertyCondition).PropertyName;
        return (propertyName, ResolveActivityProperty(activity, propertyName));
    }

    private static string CustomText(string fallback, string? en, string? tr)
    {
        return string.Join('\u001f', fallback, en ?? string.Empty, tr ?? string.Empty);
    }
}

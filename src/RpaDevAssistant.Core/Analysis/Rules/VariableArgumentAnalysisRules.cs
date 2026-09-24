using System.Text.RegularExpressions;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis.Rules;

public sealed partial class ArgumentNamingConventionRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA031";

    public override string Name => "Argument Naming Convention";

    public override string Description => "Checks whether workflow argument names use the prefix that matches their direction.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Naming;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        var convention = context.RuleConfiguration?.NamingConvention;
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var argument in workflow.Arguments)
            {
                var expectedPrefix = argument.Direction?.ToUpperInvariant() switch
                {
                    "IN" => convention?.InPrefix ?? "in_",
                    "OUT" => convention?.OutPrefix ?? "out_",
                    "INOUT" => convention?.InOutPrefix ?? "io_",
                    _ => null
                };

                if (expectedPrefix is null || argument.Name.StartsWith(expectedPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                yield return CreateFinding(
                    $"Argument '{argument.Name}' does not use the '{expectedPrefix}' prefix for direction {argument.Direction}.",
                    $"Rename the argument to use the '{expectedPrefix}' prefix without changing its direction or type.",
                    workflow,
                    propertyName: "ArgumentName",
                    currentValue: argument.Name) with { Scope = UiPathFindingScope.Workflow };
            }
        }
    }
}

public sealed partial class VariableNamingConventionRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA032";

    public override string Name => "Variable Naming Convention";

    public override string Description => "Checks whether workflow variable names use lowerCamelCase.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Naming;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        var pattern = CreatePattern(context.RuleConfiguration?.NamingConvention?.Pattern);
        var requiredPrefix = context.RuleConfiguration?.NamingConvention?.RequiredPrefix;
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var variable in workflow.Variables.Where(variable =>
                         !pattern.IsMatch(variable.Name)
                         || (!string.IsNullOrWhiteSpace(requiredPrefix)
                             && !variable.Name.StartsWith(requiredPrefix, StringComparison.Ordinal))))
            {
                yield return CreateFinding(
                    $"Variable '{variable.Name}' does not use lowerCamelCase.",
                    "Rename the variable with a descriptive lowerCamelCase name.",
                    workflow,
                    propertyName: "VariableName",
                    currentValue: variable.Name) with { Scope = UiPathFindingScope.Workflow };
            }
        }
    }

    private static Regex CreatePattern(string? configuredPattern)
    {
        if (string.IsNullOrWhiteSpace(configuredPattern))
        {
            return LowerCamelCase();
        }

        try
        {
            return new Regex(configuredPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        catch (ArgumentException)
        {
            return LowerCamelCase();
        }
    }

    [GeneratedRegex("^[a-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex LowerCamelCase();
}

public interface IUiPathSymbolUsageAnalyzer
{
    IReadOnlySet<string> FindReferencedSymbols(UiPathWorkflowAnalysis workflow);

    bool IsVariableReferenced(UiPathWorkflowAnalysis workflow, UiPathVariableInfo variable);

    IReadOnlyList<string> FindVariableReferenceActivityIds(UiPathWorkflowAnalysis workflow, UiPathVariableInfo variable);

    UiPathSymbolAccess GetArgumentAccess(UiPathWorkflowAnalysis workflow, string argumentName);
}

[Flags]
public enum UiPathSymbolAccess
{
    None = 0,
    Read = 1,
    Write = 2
}

public sealed partial class UiPathSymbolUsageAnalyzer : IUiPathSymbolUsageAnalyzer
{
    private static readonly HashSet<string> IgnoredProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "DisplayName",
        "TypeArguments",
        "IdRef"
    };

    public IReadOnlySet<string> FindReferencedSymbols(UiPathWorkflowAnalysis workflow)
    {
        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var activitiesById = workflow.Activities.ToDictionary(activity => activity.ActivityId, StringComparer.OrdinalIgnoreCase);
        foreach (var activity in workflow.Activities.Where(activity => !IsCommentedOut(activity, activitiesById)))
        {
            AddActivitySymbols(activity, symbols);
        }

        foreach (var variable in workflow.Variables.Where(variable => !string.IsNullOrWhiteSpace(variable.DefaultValue)))
        {
            AddSymbols(variable.DefaultValue!, symbols);
        }

        return symbols;
    }

    public bool IsVariableReferenced(UiPathWorkflowAnalysis workflow, UiPathVariableInfo variable)
    {
        if (string.IsNullOrWhiteSpace(variable.ScopeActivityId))
        {
            return FindReferencedSymbols(workflow).Contains(variable.Name);
        }

        if (!workflow.Activities.Any(activity => activity.ActivityId.Equals(variable.ScopeActivityId, StringComparison.OrdinalIgnoreCase)))
        {
            return FindReferencedSymbols(workflow).Contains(variable.Name);
        }

        return FindVariableReferenceActivityIds(workflow, variable).Count > 0;
    }

    public IReadOnlyList<string> FindVariableReferenceActivityIds(UiPathWorkflowAnalysis workflow, UiPathVariableInfo variable)
    {
        if (string.IsNullOrWhiteSpace(variable.ScopeActivityId))
        {
            return [];
        }

        var activitiesById = workflow.Activities.ToDictionary(activity => activity.ActivityId, StringComparer.OrdinalIgnoreCase);
        if (!activitiesById.ContainsKey(variable.ScopeActivityId))
        {
            return [];
        }

        var nestedShadowScopes = workflow.Variables
            .Where(candidate => !ReferenceEquals(candidate, variable)
                && candidate.Name.Equals(variable.Name, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(candidate.ScopeActivityId)
                && UiPathVariableScopeGraph.IsWithin(candidate.ScopeActivityId!, variable.ScopeActivityId, activitiesById))
            .Select(candidate => candidate.ScopeActivityId!)
            .ToArray();

        var references = new List<string>();
        foreach (var activity in workflow.Activities.Where(activity =>
                     !IsCommentedOut(activity, activitiesById)
                     && UiPathVariableScopeGraph.IsWithin(activity.ActivityId, variable.ScopeActivityId, activitiesById)
                     && !nestedShadowScopes.Any(scope => UiPathVariableScopeGraph.IsWithin(activity.ActivityId, scope, activitiesById))))
        {
            var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddActivitySymbols(activity, symbols);
            if (symbols.Contains(variable.Name))
            {
                references.Add(activity.ActivityId);
            }
        }

        foreach (var candidate in workflow.Variables.Where(candidate =>
                     !ReferenceEquals(candidate, variable)
                     && !string.IsNullOrWhiteSpace(candidate.DefaultValue)
                     && !string.IsNullOrWhiteSpace(candidate.ScopeActivityId)
                     && UiPathVariableScopeGraph.IsWithin(candidate.ScopeActivityId!, variable.ScopeActivityId, activitiesById)
                     && !nestedShadowScopes.Any(scope => UiPathVariableScopeGraph.IsWithin(candidate.ScopeActivityId!, scope, activitiesById))
                     && ContainsSymbol(candidate.DefaultValue!, variable.Name)))
        {
            references.Add(candidate.ScopeActivityId!);
        }

        return references.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public UiPathSymbolAccess GetArgumentAccess(UiPathWorkflowAnalysis workflow, string argumentName)
    {
        var access = UiPathSymbolAccess.None;
        var activitiesById = workflow.Activities.ToDictionary(activity => activity.ActivityId, StringComparer.OrdinalIgnoreCase);
        foreach (var activity in workflow.Activities.Where(activity => !IsCommentedOut(activity, activitiesById)))
        {
            foreach (var property in activity.Properties)
            {
                if (IgnoredProperties.Contains(property.Key)
                    || string.IsNullOrWhiteSpace(property.Value)
                    || property.Key.Equals("Selector", StringComparison.OrdinalIgnoreCase)
                        && property.Value.TrimStart().StartsWith('<')
                    || !ContainsSymbol(property.Value, argumentName))
                {
                    continue;
                }

                access |= IsWriteTargetProperty(activity, property.Key) ? UiPathSymbolAccess.Write : UiPathSymbolAccess.Read;
            }

            foreach (var mapping in activity.Arguments.Where(mapping =>
                         !string.IsNullOrWhiteSpace(mapping.Value) && ContainsSymbol(mapping.Value!, argumentName)))
            {
                if (mapping.Key.StartsWith("in_", StringComparison.OrdinalIgnoreCase))
                {
                    access |= UiPathSymbolAccess.Read;
                }
                else if (mapping.Key.StartsWith("out_", StringComparison.OrdinalIgnoreCase))
                {
                    access |= UiPathSymbolAccess.Write;
                }
                else if (mapping.Key.StartsWith("io_", StringComparison.OrdinalIgnoreCase))
                {
                    access |= UiPathSymbolAccess.Read | UiPathSymbolAccess.Write;
                }
            }
        }

        return access;
    }

    private static void AddActivitySymbols(UiPathActivityInfo activity, ISet<string> symbols)
    {
        foreach (var property in activity.Properties.Concat(activity.Arguments))
        {
            if (IgnoredProperties.Contains(property.Key) || string.IsNullOrWhiteSpace(property.Value))
            {
                continue;
            }

            var value = property.Value.Trim();
            if (property.Key.Equals("Selector", StringComparison.OrdinalIgnoreCase) && value.StartsWith('<'))
            {
                continue;
            }

            AddSymbols(value, symbols);
        }
    }

    private static void AddSymbols(string value, ISet<string> symbols)
    {
        var expression = StringLiteral().Replace(value, " ");
        foreach (Match match in Symbol().Matches(expression))
        {
            symbols.Add(match.Value);
        }
    }

    private static bool ContainsSymbol(string value, string symbol)
    {
        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddSymbols(value, symbols);
        return symbols.Contains(symbol);
    }

    private static bool IsWriteTargetProperty(UiPathActivityInfo activity, string propertyName)
    {
        return propertyName.Equals("To", StringComparison.OrdinalIgnoreCase)
                && UiPathActivityClassifier.IsNamed(activity, "Assign", "MultipleAssign")
            || propertyName.Equals("Result", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("Output", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("OutputDataTable", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("Response", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("StatusCode", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("ExtractedData", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCommentedOut(
        UiPathActivityInfo activity,
        IReadOnlyDictionary<string, UiPathActivityInfo> activitiesById)
    {
        UiPathActivityInfo? current = activity;
        while (current is not null)
        {
            if (current.Name.Equals("CommentOut", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = !string.IsNullOrWhiteSpace(current.ParentActivityId)
                && activitiesById.TryGetValue(current.ParentActivityId, out var parent)
                    ? parent
                    : null;
        }

        return false;
    }

    [GeneratedRegex("\"(?:\"\"|[^\"])*\"", RegexOptions.CultureInvariant)]
    private static partial Regex StringLiteral();

    [GeneratedRegex("(?<![A-Za-z0-9_.])[A-Za-z_][A-Za-z0-9_]*", RegexOptions.CultureInvariant)]
    private static partial Regex Symbol();
}

internal static class UiPathVariableScopeGraph
{
    private static readonly string[] VariableScopeActivities = ["Sequence", "Flowchart", "StateMachine"];

    public static bool TryBuild(
        UiPathWorkflowAnalysis workflow,
        out IReadOnlyDictionary<string, UiPathActivityInfo> activitiesById)
    {
        if (workflow.Activities.Any(activity => string.IsNullOrWhiteSpace(activity.ActivityId)))
        {
            activitiesById = new Dictionary<string, UiPathActivityInfo>();
            return false;
        }

        var groups = workflow.Activities
            .GroupBy(activity => activity.ActivityId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (groups.Any(group => group.Count() != 1))
        {
            activitiesById = new Dictionary<string, UiPathActivityInfo>();
            return false;
        }

        activitiesById = groups.ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
        foreach (var activity in workflow.Activities)
        {
            var current = activity;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (visited.Add(current.ActivityId))
            {
                if (string.IsNullOrWhiteSpace(current.ParentActivityId))
                {
                    break;
                }

                if (!activitiesById.TryGetValue(current.ParentActivityId, out current))
                {
                    activitiesById = new Dictionary<string, UiPathActivityInfo>();
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(current.ParentActivityId))
            {
                activitiesById = new Dictionary<string, UiPathActivityInfo>();
                return false;
            }
        }

        return true;
    }

    public static bool IsWithin(
        string activityId,
        string scopeActivityId,
        IReadOnlyDictionary<string, UiPathActivityInfo> activitiesById)
    {
        var currentId = activityId;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (visited.Add(currentId) && activitiesById.TryGetValue(currentId, out var current))
        {
            if (current.ActivityId.Equals(scopeActivityId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(current.ParentActivityId))
            {
                break;
            }

            currentId = current.ParentActivityId;
        }

        return false;
    }

    public static UiPathActivityInfo? FindNarrowerCommonScope(
        string declaredScopeId,
        IReadOnlyList<string> referenceActivityIds,
        IReadOnlyDictionary<string, UiPathActivityInfo> activitiesById)
    {
        if (referenceActivityIds.Count == 0)
        {
            return null;
        }

        var currentId = referenceActivityIds[0];
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (visited.Add(currentId) && activitiesById.TryGetValue(currentId, out var current))
        {
            if (current.ActivityId.Equals(declaredScopeId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (UiPathActivityClassifier.IsNamed(current, VariableScopeActivities)
                && referenceActivityIds.All(referenceId => IsWithin(referenceId, current.ActivityId, activitiesById)))
            {
                return current;
            }

            if (string.IsNullOrWhiteSpace(current.ParentActivityId))
            {
                return null;
            }

            currentId = current.ParentActivityId;
        }

        return null;
    }
}

public sealed class UnusedVariableRule : UiPathAnalysisRuleBase
{
    private readonly IUiPathSymbolUsageAnalyzer usageAnalyzer;

    public UnusedVariableRule(IUiPathSymbolUsageAnalyzer usageAnalyzer) => this.usageAnalyzer = usageAnalyzer;

    public override string Id => "RPA033";
    public override string Name => "Unused Variable";
    public override string Description => "Detects declared workflow variables that are not referenced by parsed activity expressions.";
    public override RuleSeverity Severity => RuleSeverity.Suggestion;
    public override RuleCategory Category => RuleCategory.Maintainability;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            var referenced = usageAnalyzer.FindReferencedSymbols(workflow);
            var groups = workflow.Variables.GroupBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
            foreach (var variable in workflow.Variables)
            {
                var sameName = groups[variable.Name];
                var scopesAreReliable = sameName.Length == 1 || sameName.All(item => !string.IsNullOrWhiteSpace(item.ScopeActivityId))
                    && sameName.Select(item => item.ScopeActivityId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == sameName.Length;
                if (!scopesAreReliable || (sameName.Length == 1 ? referenced.Contains(variable.Name) : usageAnalyzer.IsVariableReferenced(workflow, variable)))
                {
                    continue;
                }

                yield return CreateFinding(
                    $"Variable '{variable.Name}' is declared but no parsed activity expression references it.",
                    "Confirm the variable is unused, then remove it to simplify the workflow.",
                    workflow,
                    propertyName: "VariableName",
                    currentValue: variable.Name) with { Scope = UiPathFindingScope.Workflow };
            }
        }
    }
}

public sealed class UnusedArgumentRule : UiPathAnalysisRuleBase
{
    private readonly IUiPathSymbolUsageAnalyzer usageAnalyzer;

    public UnusedArgumentRule(IUiPathSymbolUsageAnalyzer usageAnalyzer) => this.usageAnalyzer = usageAnalyzer;

    public override string Id => "RPA034";
    public override string Name => "Unused Argument";
    public override string Description => "Detects declared workflow arguments that are not referenced by parsed activity expressions.";
    public override RuleSeverity Severity => RuleSeverity.Suggestion;
    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        var mappedArgumentsByWorkflow = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var runtimeMappedWorkflows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var invocation in UiPathWorkflowInvocationResolver.Resolve(context))
        {
            var calleePath = UiPathWorkflowInvocationResolver.NormalizePath(invocation.Callee.RelativePath);
            if (invocation.UsesArgumentsVariable)
            {
                runtimeMappedWorkflows.Add(calleePath);
            }

            if (!mappedArgumentsByWorkflow.TryGetValue(calleePath, out var mappedArguments))
            {
                mappedArguments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                mappedArgumentsByWorkflow[calleePath] = mappedArguments;
            }

            mappedArguments.UnionWith(invocation.Activity.Arguments.Keys);
        }

        foreach (var workflow in context.WorkflowAnalyses)
        {
            var referenced = usageAnalyzer.FindReferencedSymbols(workflow);
            var workflowPath = UiPathWorkflowInvocationResolver.NormalizePath(workflow.RelativePath);
            if (runtimeMappedWorkflows.Contains(workflowPath))
            {
                continue;
            }

            mappedArgumentsByWorkflow.TryGetValue(workflowPath, out var externallyMapped);
            foreach (var argument in workflow.Arguments.Where(argument =>
                         !referenced.Contains(argument.Name)
                         && externallyMapped?.Contains(argument.Name) != true))
            {
                yield return CreateFinding(
                    $"Argument '{argument.Name}' is declared but no parsed activity expression references it.",
                    "Confirm the argument is not part of a required caller contract before removing it.",
                    workflow,
                    propertyName: "ArgumentName",
                    currentValue: argument.Name) with { Scope = UiPathFindingScope.Workflow };
            }
        }
    }

}

internal sealed record UiPathResolvedWorkflowInvocation(
    UiPathWorkflowAnalysis Caller,
    UiPathActivityInfo Activity,
    UiPathWorkflowAnalysis Callee,
    bool UsesArgumentsVariable);

internal static class UiPathWorkflowInvocationResolver
{
    public static IReadOnlyList<UiPathResolvedWorkflowInvocation> Resolve(UiPathAnalysisContext context)
    {
        var workflowsByPath = context.WorkflowAnalyses
            .GroupBy(workflow => NormalizePath(workflow.RelativePath), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
        var invocations = new List<UiPathResolvedWorkflowInvocation>();

        foreach (var caller in context.WorkflowAnalyses)
        {
            foreach (var activity in caller.Activities.Where(item => UiPathActivityClassifier.IsNamed(item, "InvokeWorkflowFile")))
            {
                if (!UiPathPropertyLookup.TryGet(activity, out var rawPath, "WorkflowFileName", "WorkflowFile", "FileName")
                    || string.IsNullOrWhiteSpace(rawPath)
                    || IsDynamicReference(rawPath))
                {
                    continue;
                }

                var calleePath = ResolvePath(caller.RelativePath, rawPath, workflowsByPath.Keys);
                if (calleePath is null || !workflowsByPath.TryGetValue(calleePath, out var callee))
                {
                    continue;
                }

                var usesArgumentsVariable = UiPathPropertyLookup.TryGet(activity, out var argumentsVariable, "ArgumentsVariable")
                    && IsRuntimeArgumentsVariable(argumentsVariable);
                invocations.Add(new UiPathResolvedWorkflowInvocation(caller, activity, callee, usesArgumentsVariable));
            }
        }

        return invocations;
    }

    public static string NormalizePath(string path)
    {
        var segments = path.Trim().Trim('"', '\'').Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = new List<string>();
        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == ".." && normalized.Count > 0)
            {
                normalized.RemoveAt(normalized.Count - 1);
                continue;
            }

            normalized.Add(segment);
        }

        return string.Join('/', normalized);
    }

    private static string? ResolvePath(string callerPath, string referencedPath, IEnumerable<string> knownPaths)
    {
        var known = knownPaths as IReadOnlyCollection<string> ?? knownPaths.ToArray();
        var normalizedReference = NormalizePath(referencedPath);
        if (known.Contains(normalizedReference, StringComparer.OrdinalIgnoreCase))
        {
            return normalizedReference;
        }

        var normalizedCallerPath = NormalizePath(callerPath);
        var separatorIndex = normalizedCallerPath.LastIndexOf('/');
        var callerDirectory = separatorIndex >= 0 ? normalizedCallerPath[..separatorIndex] : string.Empty;
        var relativeReference = NormalizePath(string.IsNullOrWhiteSpace(callerDirectory)
            ? referencedPath
            : $"{callerDirectory}/{referencedPath}");
        return known.Contains(relativeReference, StringComparer.OrdinalIgnoreCase) ? relativeReference : null;
    }

    private static bool IsDynamicReference(string value)
    {
        var trimmed = value.Trim();
        return !trimmed.Trim('"', '\'').EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("[", StringComparison.Ordinal)
            || trimmed.Contains('+', StringComparison.Ordinal)
            || trimmed.Contains('&', StringComparison.Ordinal)
            || trimmed.Contains('(', StringComparison.Ordinal)
            || trimmed.Contains(')', StringComparison.Ordinal)
            || trimmed.Contains('$', StringComparison.Ordinal)
            || trimmed.Contains('{', StringComparison.Ordinal)
            || trimmed.Contains('}', StringComparison.Ordinal);
    }

    private static bool IsRuntimeArgumentsVariable(string? value)
    {
        var trimmed = value?.Trim();
        return !string.IsNullOrWhiteSpace(trimmed)
            && !trimmed.Equals("{x:Null}", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Equals("x:Null", StringComparison.OrdinalIgnoreCase);
    }
}

internal static class UiPathWorkflowArgumentContract
{
    public static IReadOnlyDictionary<string, UiPathArgumentInfo> GetUnambiguousArguments(UiPathWorkflowAnalysis workflow) =>
        workflow.Arguments
            .GroupBy(argument => argument.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);

    public static string? GetDeclaredDirection(UiPathArgumentInfo argument)
    {
        if (string.IsNullOrWhiteSpace(argument.Type))
        {
            return null;
        }

        if (argument.Type.StartsWith("InOutArgument", StringComparison.OrdinalIgnoreCase))
        {
            return "InOut";
        }

        if (argument.Type.StartsWith("InArgument", StringComparison.OrdinalIgnoreCase))
        {
            return "In";
        }

        return argument.Type.StartsWith("OutArgument", StringComparison.OrdinalIgnoreCase) ? "Out" : null;
    }
}

public sealed class InvalidInvokeWorkflowArgumentMappingRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA043";
    public override string Name => "Invalid Invoke Workflow Argument Mapping";
    public override string Description => "Detects static Invoke Workflow File mappings whose key is not declared by the target workflow.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var invocation in UiPathWorkflowInvocationResolver.Resolve(context).Where(item => !item.UsesArgumentsVariable))
        {
            var declaredArguments = invocation.Callee.Arguments.Select(argument => argument.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var key in invocation.Activity.Arguments.Keys.Where(key => !declaredArguments.Contains(key)))
            {
                yield return CreateFinding(
                    $"Invoke mapping '{key}' is not declared by workflow '{invocation.Callee.RelativePath}'.",
                    "Remove the mapping or update it to an argument declared by the target workflow.",
                    invocation.Caller,
                    invocation.Activity,
                    propertyName: "ArgumentMapping",
                    currentValue: key);
            }
        }
    }
}

public sealed class MissingInvokeWorkflowArgumentMappingRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA044";
    public override string Name => "Missing Invoke Workflow Argument Mapping";
    public override string Description => "Detects input arguments without a serialized default value that are not mapped by a static Invoke Workflow File activity.";
    public override RuleSeverity Severity => RuleSeverity.Suggestion;
    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var invocation in UiPathWorkflowInvocationResolver.Resolve(context).Where(item => !item.UsesArgumentsVariable))
        {
            var mappedArguments = invocation.Activity.Arguments.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var argument in UiPathWorkflowArgumentContract.GetUnambiguousArguments(invocation.Callee).Values.Where(argument =>
                         IsInputDirection(UiPathWorkflowArgumentContract.GetDeclaredDirection(argument))
                         && !argument.HasDefaultValue
                         && !mappedArguments.Contains(argument.Name)))
            {
                yield return CreateFinding(
                    $"Input argument '{argument.Name}' is not mapped when invoking '{invocation.Callee.RelativePath}'.",
                    "Review the target workflow contract and map the input when it does not have an intentional default value.",
                    invocation.Caller,
                    invocation.Activity,
                    propertyName: "ArgumentMapping",
                    currentValue: argument.Name);
            }
        }
    }

    private static bool IsInputDirection(string? direction) =>
        direction?.Equals("In", StringComparison.OrdinalIgnoreCase) == true
        || direction?.Equals("InOut", StringComparison.OrdinalIgnoreCase) == true;
}

public sealed class InvokeWorkflowArgumentDirectionMismatchRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA045";
    public override string Name => "Invoke Workflow Argument Direction Mismatch";
    public override string Description => "Detects static Invoke Workflow File mappings whose wrapper direction conflicts with the target argument direction.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var invocation in UiPathWorkflowInvocationResolver.Resolve(context).Where(item => !item.UsesArgumentsVariable))
        {
            var arguments = UiPathWorkflowArgumentContract.GetUnambiguousArguments(invocation.Callee);
            foreach (var mapping in invocation.Activity.ArgumentMappingDirections)
            {
                if (string.IsNullOrWhiteSpace(mapping.Value)
                    || !arguments.TryGetValue(mapping.Key, out var targetArgument)
                    || UiPathWorkflowArgumentContract.GetDeclaredDirection(targetArgument) is not { } targetDirection
                    || mapping.Value.Equals(targetDirection, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return CreateFinding(
                    $"Invoke mapping '{mapping.Key}' uses {mapping.Value}, but the target workflow declares it as {targetDirection}.",
                    "Align the Invoke Workflow File mapping direction with the target workflow argument contract.",
                    invocation.Caller,
                    invocation.Activity,
                    propertyName: "ArgumentDirection",
                    currentValue: mapping.Value);
            }
        }
    }
}

public sealed class ArgumentDirectionMismatchRule : UiPathAnalysisRuleBase
{
    private readonly IUiPathSymbolUsageAnalyzer usageAnalyzer;

    public ArgumentDirectionMismatchRule(IUiPathSymbolUsageAnalyzer usageAnalyzer) => this.usageAnalyzer = usageAnalyzer;

    public override string Id => "RPA038";
    public override string Name => "Argument Direction Mismatch";
    public override string Description => "Detects argument access that conflicts with the declared In or Out direction.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var argument in workflow.Arguments)
            {
                var access = usageAnalyzer.GetArgumentAccess(workflow, argument.Name);
                var direction = argument.Direction?.ToUpperInvariant();
                var mismatch = direction switch
                {
                    "IN" => access.HasFlag(UiPathSymbolAccess.Write),
                    "OUT" => access == UiPathSymbolAccess.Read,
                    _ => false
                };
                if (!mismatch)
                {
                    continue;
                }

                var recommendation = direction == "IN"
                    ? "Use InOut when the caller value must be read and updated, or avoid writing to the In argument."
                    : "Use In when the workflow only reads the caller value, or assign a result before returning an Out argument.";
                yield return CreateFinding(
                    $"Argument '{argument.Name}' is declared as {argument.Direction}, but its parsed access is {DescribeAccess(access)}.",
                    recommendation,
                    workflow,
                    propertyName: "ArgumentDirection",
                    currentValue: argument.Direction) with { Scope = UiPathFindingScope.Workflow };
            }
        }
    }

    private static string DescribeAccess(UiPathSymbolAccess access) => access switch
    {
        UiPathSymbolAccess.Read => "read-only",
        UiPathSymbolAccess.Write => "write-only",
        UiPathSymbolAccess.Read | UiPathSymbolAccess.Write => "read/write",
        _ => "unused"
    };
}

public sealed class UnnecessaryInOutArgumentRule : UiPathAnalysisRuleBase
{
    private readonly IUiPathSymbolUsageAnalyzer usageAnalyzer;

    public UnnecessaryInOutArgumentRule(IUiPathSymbolUsageAnalyzer usageAnalyzer) => this.usageAnalyzer = usageAnalyzer;

    public override string Id => "RPA039";
    public override string Name => "Unnecessary InOut Argument";
    public override string Description => "Detects InOut arguments that are only read or only written by the workflow.";
    public override RuleSeverity Severity => RuleSeverity.Suggestion;
    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var argument in workflow.Arguments.Where(argument =>
                         argument.Direction?.Equals("InOut", StringComparison.OrdinalIgnoreCase) == true))
            {
                var access = usageAnalyzer.GetArgumentAccess(workflow, argument.Name);
                var suggestedDirection = access switch
                {
                    UiPathSymbolAccess.Read => "In",
                    UiPathSymbolAccess.Write => "Out",
                    _ => null
                };
                if (suggestedDirection is null)
                {
                    continue;
                }

                yield return CreateFinding(
                    $"InOut argument '{argument.Name}' is {DescribeAccess(access)} and can use the narrower {suggestedDirection} direction.",
                    $"Change the argument direction to {suggestedDirection} after verifying its caller mappings.",
                    workflow,
                    propertyName: "ArgumentDirection",
                    currentValue: argument.Direction) with { Scope = UiPathFindingScope.Workflow };
            }
        }
    }

    private static string DescribeAccess(UiPathSymbolAccess access) =>
        access == UiPathSymbolAccess.Read ? "read-only" : "write-only";
}

public sealed class InvalidArgumentTypeDeclarationRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA040";
    public override string Name => "Invalid Argument Type Declaration";
    public override string Description => "Detects missing or malformed workflow argument type declarations.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var argument in workflow.Arguments.Where(argument => !HasValidTypeDeclaration(argument.Type)))
            {
                yield return CreateFinding(
                    $"Argument '{argument.Name}' has a missing or malformed type declaration.",
                    "Declare the argument with a valid InArgument, OutArgument, or InOutArgument type.",
                    workflow,
                    propertyName: "ArgumentType",
                    currentValue: string.IsNullOrWhiteSpace(argument.Type) ? "[missing]" : argument.Type) with
                {
                    Scope = UiPathFindingScope.Workflow
                };
            }
        }
    }

    private static bool HasValidTypeDeclaration(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        var declaration = type.Trim();
        var wrapperLength = new[] { "InArgument", "OutArgument", "InOutArgument" }
            .Where(wrapper => declaration.StartsWith(wrapper, StringComparison.OrdinalIgnoreCase))
            .Select(wrapper => wrapper.Length)
            .FirstOrDefault();
        if (wrapperLength == 0)
        {
            return false;
        }

        var payload = declaration[wrapperLength..].Trim();
        if (payload.Length < 3 || payload[0] != '(' || payload[^1] != ')')
        {
            return false;
        }

        var typeExpression = payload[1..^1];
        var index = 0;
        return ParseTypeExpression(typeExpression, ref index) && SkipWhitespace(typeExpression, ref index) == typeExpression.Length;
    }

    private static bool ParseTypeExpression(string value, ref int index)
    {
        SkipWhitespace(value, ref index);
        var identifierStart = index;
        while (index < value.Length && IsTypeIdentifierCharacter(value[index]))
        {
            index++;
        }

        if (index == identifierStart)
        {
            return false;
        }

        SkipWhitespace(value, ref index);
        if (index < value.Length && value[index] == '(')
        {
            index++;
            if (!ParseTypeExpression(value, ref index))
            {
                return false;
            }

            SkipWhitespace(value, ref index);
            while (index < value.Length && value[index] == ',')
            {
                index++;
                if (!ParseTypeExpression(value, ref index))
                {
                    return false;
                }

                SkipWhitespace(value, ref index);
            }

            if (index >= value.Length || value[index] != ')')
            {
                return false;
            }

            index++;
        }

        SkipWhitespace(value, ref index);
        while (index + 1 < value.Length && value[index] == '[' && value[index + 1] == ']')
        {
            index += 2;
            SkipWhitespace(value, ref index);
        }

        if (index < value.Length && value[index] == '?')
        {
            index++;
        }

        return true;
    }

    private static int SkipWhitespace(string value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsTypeIdentifierCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '_' or '.' or ':' or '+' or '`';
}

public sealed class OverlyBroadVariableScopeRule : UiPathAnalysisRuleBase
{
    private readonly IUiPathSymbolUsageAnalyzer usageAnalyzer;

    public OverlyBroadVariableScopeRule(IUiPathSymbolUsageAnalyzer usageAnalyzer) => this.usageAnalyzer = usageAnalyzer;

    public override string Id => "RPA041";
    public override string Name => "Overly Broad Variable Scope";
    public override string Description => "Detects variables whose references are contained within a narrower stable workflow scope.";
    public override RuleSeverity Severity => RuleSeverity.Suggestion;
    public override RuleCategory Category => RuleCategory.Maintainability;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            if (!UiPathVariableScopeGraph.TryBuild(workflow, out var activitiesById))
            {
                continue;
            }

            var names = workflow.Variables.GroupBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            foreach (var variable in workflow.Variables.Where(variable =>
                         names[variable.Name] == 1 && !string.IsNullOrWhiteSpace(variable.ScopeActivityId)))
            {
                var references = usageAnalyzer.FindVariableReferenceActivityIds(workflow, variable);
                var narrowerScope = UiPathVariableScopeGraph.FindNarrowerCommonScope(
                    variable.ScopeActivityId!, references, activitiesById);
                if (narrowerScope is null)
                {
                    continue;
                }

                yield return CreateFinding(
                    $"Variable '{variable.Name}' is only used inside the narrower scope '{narrowerScope.DisplayName}'.",
                    "Move the variable declaration to the narrowest shared Sequence, Flowchart, or StateMachine scope that contains all references.",
                    workflow,
                    narrowerScope,
                    propertyName: "VariableScope",
                    currentValue: variable.Scope ?? variable.ScopeActivityId) with { Scope = UiPathFindingScope.Workflow };
            }
        }
    }
}

public sealed class ShadowedVariableRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA042";
    public override string Name => "Shadowed Variable";
    public override string Description => "Detects nested variable declarations that reuse a name from an ancestor scope.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override RuleCategory Category => RuleCategory.Maintainability;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            if (!UiPathVariableScopeGraph.TryBuild(workflow, out var activitiesById))
            {
                continue;
            }

            foreach (var group in workflow.Variables.GroupBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase))
            {
                var scopedVariables = group.Where(variable =>
                    !string.IsNullOrWhiteSpace(variable.ScopeActivityId)
                    && activitiesById.ContainsKey(variable.ScopeActivityId)).ToArray();
                foreach (var variable in scopedVariables.Where(variable => scopedVariables.Any(ancestor =>
                             !ReferenceEquals(ancestor, variable)
                             && !ancestor.ScopeActivityId!.Equals(variable.ScopeActivityId, StringComparison.OrdinalIgnoreCase)
                             && UiPathVariableScopeGraph.IsWithin(variable.ScopeActivityId!, ancestor.ScopeActivityId, activitiesById))))
                {
                    yield return CreateFinding(
                        $"Variable '{variable.Name}' shadows a declaration from an ancestor scope.",
                        "Rename the nested variable or reuse the ancestor declaration to make references unambiguous.",
                        workflow,
                        activitiesById[variable.ScopeActivityId!],
                        propertyName: "VariableName",
                        currentValue: variable.Name) with { Scope = UiPathFindingScope.Workflow };
                }
            }
        }
    }
}

using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis.Rules;

public sealed class HardCodedQueueNameRule : UiPathAnalysisRuleBase
{
    private static readonly string[] QueueActivityNames =
    [
        "AddQueueItem",
        "AddQueueItemAndGetReference",
        "BulkAddQueueItems",
        "DeleteQueueItems",
        "GetQueueItem",
        "GetQueueItems",
        "GetTransactionItem",
        "SetTransactionProgress",
        "SetTransactionStatus"
    ];

    private readonly IUiPathExpressionClassifier expressionClassifier;

    public HardCodedQueueNameRule(IUiPathExpressionClassifier expressionClassifier)
    {
        this.expressionClassifier = expressionClassifier;
    }

    public override string Id => "RPA047";
    public override string Name => "Hard-Coded Queue Name";
    public override string Description => "Detects literal QueueName values in UiPath Orchestrator Queue activities.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override RuleCategory Category => RuleCategory.Orchestrator;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities.Where(activity =>
                         UiPathActivityClassifier.IsNamed(activity, QueueActivityNames)))
            {
                if (!UiPathPropertyLookup.TryGet(activity, out var queueName, "QueueName", "Queue")
                    || !IsMeaningfulLiteral(queueName, out var literal))
                {
                    continue;
                }

                yield return CreateFinding(
                    $"Queue activity uses the hard-coded queue name '{literal}'.",
                    "Move environment-specific Queue names to Config or an Orchestrator Asset.",
                    workflow,
                    activity,
                    propertyName: "QueueName",
                    currentValue: literal);
            }
        }
    }

    private bool IsMeaningfulLiteral(string? value, out string literal)
    {
        literal = string.Empty;
        var classification = expressionClassifier.Classify(value);
        if (classification.Kind != UiPathExpressionKind.LiteralString
            || string.IsNullOrWhiteSpace(classification.LiteralValue))
        {
            return false;
        }

        literal = classification.LiteralValue.Trim();
        return !literal.Equals("Nothing", StringComparison.OrdinalIgnoreCase)
            && !literal.Equals("null", StringComparison.OrdinalIgnoreCase)
            && !literal.Equals("{x:Null}", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class InvalidArgumentDefaultValueRule : UiPathAnalysisRuleBase
{
    private static readonly HashSet<string> NonNullableValueTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Boolean", "Byte", "SByte", "Char", "DateTime", "Decimal", "Double", "Guid",
        "Int16", "Int32", "Int64", "Single", "TimeSpan", "UInt16", "UInt32", "UInt64",
        "bool", "byte", "sbyte", "char", "decimal", "double", "short", "int", "long",
        "float", "ushort", "uint", "ulong",
        "x:Boolean", "x:Byte", "x:Char", "x:DateTime", "x:Decimal", "x:Double",
        "x:Int16", "x:Int32", "x:Int64", "x:Single"
    };

    public override string Id => "RPA048";
    public override string Name => "Invalid Argument Default Value";
    public override string Description => "Detects argument defaults that conflict with a reliable workflow argument contract.";
    public override RuleSeverity Severity => RuleSeverity.Warning;
    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var argument in workflow.Arguments.Where(argument => argument.HasDefaultValue))
            {
                var direction = UiPathWorkflowArgumentContract.GetDeclaredDirection(argument);
                var issue = GetIssue(argument, direction);
                if (issue is null)
                {
                    continue;
                }

                yield return CreateFinding(
                    issue.Value.Message,
                    issue.Value.Recommendation,
                    workflow,
                    propertyName: "ArgumentDefaultValue",
                    currentValue: string.IsNullOrWhiteSpace(argument.DefaultValue) ? "[empty]" : argument.DefaultValue) with
                {
                    Scope = UiPathFindingScope.Workflow
                };
            }
        }
    }

    private static (string Message, string Recommendation)? GetIssue(UiPathArgumentInfo argument, string? direction)
    {
        if (direction?.Equals("Out", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (
                $"Out argument '{argument.Name}' declares a default value that is not part of its output contract.",
                "Remove the default from the Out argument and assign its result explicitly before the workflow returns.");
        }

        if (direction is not ("In" or "InOut"))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(argument.DefaultValue))
        {
            return (
                $"Argument '{argument.Name}' has an explicit but empty default value.",
                "Provide an intentional valid default or remove the empty default declaration.");
        }

        if (IsNullMarker(argument.DefaultValue) && IsNonNullableValueType(argument.Type))
        {
            return (
                $"Argument '{argument.Name}' uses a null default for a non-nullable value type.",
                "Use a valid value-type default or change the argument type only when null is part of the contract.");
        }

        return null;
    }

    private static bool IsNullMarker(string value)
    {
        var normalized = value.Trim().Trim('[', ']').Trim();
        return normalized.Equals("{x:Null}", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("x:Null", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Nothing", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("null", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNonNullableValueType(string? declaration)
    {
        if (string.IsNullOrWhiteSpace(declaration)
            || declaration.Contains("Nullable", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var open = declaration.IndexOf('(');
        var close = declaration.LastIndexOf(')');
        if (open < 0 || close <= open)
        {
            return false;
        }

        var type = declaration[(open + 1)..close].Trim();
        if (NonNullableValueTypes.Contains(type))
        {
            return true;
        }

        var namespaceSeparator = type.LastIndexOf('.');
        return namespaceSeparator >= 0 && NonNullableValueTypes.Contains(type[(namespaceSeparator + 1)..]);
    }
}

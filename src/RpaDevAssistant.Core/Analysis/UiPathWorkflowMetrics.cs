using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis;

public sealed record UiPathWorkflowMetrics
{
    public int TotalActivities { get; init; }

    public int ExecutableActivityCount { get; init; }

    public int ContainerActivityCount { get; init; }

    public int MaxDepth { get; init; }

    public int MaxNestingDepth => MaxDepth;

    public int ArgumentCount { get; init; }

    public int ContinueOnErrorCount { get; init; }

    public int UiActivityCount { get; init; }

    public int InvokeCount { get; init; }

    public int InvokeWorkflowCount => InvokeCount;

    public int DecisionCount { get; init; }

    public int IfCount { get; init; }

    public int SwitchCount { get; init; }

    public int LoopCount { get; init; }

    public int TryCatchCount { get; init; }

    public int ComplexityScore { get; init; }

    public UiPathWorkflowComplexityLevel ComplexityLevel { get; init; }
}

public enum UiPathWorkflowComplexityLevel
{
    Low,
    Medium,
    High,
    VeryHigh
}

public sealed record UiPathWorkflowComplexity
{
    public required string WorkflowPath { get; init; }

    public int TotalActivities { get; init; }

    public int ExecutableActivities { get; init; }

    public int ExecutableActivityCount => ExecutableActivities;

    public int ContainerActivities { get; init; }

    public int ContainerActivityCount => ContainerActivities;

    public int MaxNestingDepth { get; init; }

    public int DecisionCount { get; init; }

    public int IfCount { get; init; }

    public int SwitchCount { get; init; }

    public int LoopCount { get; init; }

    public int TryCatchCount { get; init; }

    public int InvokeWorkflowCount { get; init; }

    public int ArgumentCount { get; init; }

    public int FindingCount { get; init; }

    public int ComplexityScore { get; init; }

    public UiPathWorkflowComplexityLevel ComplexityLevel { get; init; }
}

public sealed record UiPathWorkflowComplexitySummary
{
    public int TotalWorkflowCount { get; init; }

    public int LowCount { get; init; }

    public int MediumCount { get; init; }

    public int HighCount { get; init; }

    public int VeryHighCount { get; init; }

    public IReadOnlyList<UiPathTopComplexWorkflow> TopComplexWorkflows { get; init; } = [];
}

public sealed record UiPathTopComplexWorkflow
{
    public required string WorkflowPath { get; init; }

    public int ComplexityScore { get; init; }

    public UiPathWorkflowComplexityLevel ComplexityLevel { get; init; }

    public int ExecutableActivityCount { get; init; }

    public int MaxNestingDepth { get; init; }
}

public interface IUiPathWorkflowMetricsCalculator
{
    UiPathWorkflowMetrics Calculate(UiPathWorkflowAnalysis workflow);

    UiPathWorkflowComplexity CalculateComplexity(UiPathWorkflowAnalysis workflow, int findingCount = 0);
}

public sealed class UiPathWorkflowMetricsCalculator : IUiPathWorkflowMetricsCalculator
{
    public UiPathWorkflowMetrics Calculate(UiPathWorkflowAnalysis workflow)
    {
        var executableActivities = workflow.Activities
            .Where(UiPathActivityClassifier.IsExecutable)
            .ToArray();
        var containerActivityCount = workflow.Activities.Count - executableActivities.Length;
        var ifCount = workflow.Activities.Count(activity => UiPathActivityClassifier.IsNamed(activity, "If"));
        var switchCount = workflow.Activities.Count(IsSwitchActivity);
        var loopCount = workflow.Activities.Count(IsLoopActivity);
        var tryCatchCount = workflow.Activities.Count(activity => UiPathActivityClassifier.IsNamed(activity, "TryCatch", "Try Catch"));
        var maxDepth = workflow.Activities.Count == 0 ? 0 : workflow.Activities.Max(activity => activity.Depth);
        var decisionCount = ifCount + switchCount + workflow.Activities.Count(activity => UiPathActivityClassifier.IsNamed(activity, "FlowDecision", "Flow Decision", "FlowSwitch", "Flow Switch"));
        var invokeCount = executableActivities.Count(activity => UiPathActivityClassifier.IsNamed(activity, "InvokeWorkflowFile"));
        var complexityScore = CalculateComplexityScore(
            executableActivities.Length,
            maxDepth,
            decisionCount,
            loopCount,
            tryCatchCount,
            workflow.Arguments.Count,
            invokeCount);

        return new UiPathWorkflowMetrics
        {
            TotalActivities = workflow.Activities.Count,
            ExecutableActivityCount = executableActivities.Length,
            ContainerActivityCount = Math.Max(0, containerActivityCount),
            MaxDepth = maxDepth,
            ArgumentCount = workflow.Arguments.Count,
            ContinueOnErrorCount = executableActivities.Count(HasContinueOnErrorEnabled),
            UiActivityCount = executableActivities.Count(IsUiActivity),
            InvokeCount = invokeCount,
            DecisionCount = decisionCount,
            IfCount = ifCount,
            SwitchCount = switchCount,
            LoopCount = loopCount,
            TryCatchCount = tryCatchCount,
            ComplexityScore = complexityScore,
            ComplexityLevel = CalculateComplexityLevel(complexityScore)
        };
    }

    public UiPathWorkflowComplexity CalculateComplexity(UiPathWorkflowAnalysis workflow, int findingCount = 0)
    {
        var metrics = Calculate(workflow);
        return new UiPathWorkflowComplexity
        {
            WorkflowPath = workflow.RelativePath,
            TotalActivities = metrics.TotalActivities,
            ExecutableActivities = metrics.ExecutableActivityCount,
            ContainerActivities = metrics.ContainerActivityCount,
            MaxNestingDepth = metrics.MaxNestingDepth,
            DecisionCount = metrics.DecisionCount,
            IfCount = metrics.IfCount,
            SwitchCount = metrics.SwitchCount,
            LoopCount = metrics.LoopCount,
            TryCatchCount = metrics.TryCatchCount,
            InvokeWorkflowCount = metrics.InvokeWorkflowCount,
            ArgumentCount = metrics.ArgumentCount,
            FindingCount = findingCount,
            ComplexityScore = metrics.ComplexityScore,
            ComplexityLevel = metrics.ComplexityLevel
        };
    }

    public static int CalculateComplexityScore(
        int executableActivityCount,
        int maxNestingDepth,
        int decisionCount,
        int loopCount,
        int tryCatchCount,
        int argumentCount,
        int invokeWorkflowCount)
    {
        var depthPenalty = Math.Max(0, maxNestingDepth - 3) * 4;
        var argumentPenalty = Math.Max(0, argumentCount - 5) * 2;
        var invokePenalty = Math.Min(invokeWorkflowCount, 10);

        return executableActivityCount
            + depthPenalty
            + (decisionCount * 3)
            + (loopCount * 4)
            + (tryCatchCount * 2)
            + argumentPenalty
            + invokePenalty;
    }

    public static UiPathWorkflowComplexityLevel CalculateComplexityLevel(int complexityScore)
    {
        var thresholds = UiPathAnalysisThresholds.Default;
        if (complexityScore >= thresholds.ComplexityVeryHighThreshold)
        {
            return UiPathWorkflowComplexityLevel.VeryHigh;
        }

        if (complexityScore >= thresholds.ComplexityHighThreshold)
        {
            return UiPathWorkflowComplexityLevel.High;
        }

        return complexityScore >= thresholds.ComplexityMediumThreshold
            ? UiPathWorkflowComplexityLevel.Medium
            : UiPathWorkflowComplexityLevel.Low;
    }

    public static bool HasContinueOnErrorEnabled(UiPathActivityInfo activity)
    {
        return UiPathPropertyLookup.TryGet(activity, out var value, "ContinueOnError") &&
            bool.TryParse(value, out var enabled) &&
            enabled;
    }

    public static bool IsUiActivity(UiPathActivityInfo activity)
    {
        return UiPathActivityClassifier.IsNamed(
            activity,
            "Click",
            "TypeInto",
            "Type Into",
            "GetText",
            "Get Text",
            "CheckAppState",
            "Check App State",
            "ElementExists",
            "Element Exists",
            "UseApplicationBrowser",
            "Use Application/Browser",
            "OpenBrowser",
            "Open Browser",
            "AttachBrowser",
            "Attach Browser");
    }

    private static bool IsSwitchActivity(UiPathActivityInfo activity)
    {
        return UiPathActivityClassifier.IsNamed(activity, "Switch", "FlowSwitch", "Flow Switch");
    }

    private static bool IsLoopActivity(UiPathActivityInfo activity)
    {
        return UiPathActivityClassifier.IsNamed(
            activity,
            "While",
            "DoWhile",
            "Do While",
            "ForEach",
            "For Each",
            "ForEachRow",
            "For Each Row",
            "RepeatNumberOfTimes",
            "Repeat Number Of Times");
    }
}

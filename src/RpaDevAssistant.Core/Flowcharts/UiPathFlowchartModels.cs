namespace RpaDevAssistant.Core.Flowcharts;

public sealed record UiPathFlowchartGraph
{
    public required string WorkflowPath { get; init; }

    public string? StartNodeId { get; init; }

    public IReadOnlyList<UiPathFlowNode> Nodes { get; init; } = [];

    public IReadOnlyList<UiPathFlowEdge> Edges { get; init; } = [];

    public IReadOnlyList<UiPathFlowNode> Decisions => Nodes.Where(node => node.Type == UiPathFlowNodeType.Decision).ToArray();

    public IReadOnlyList<UiPathFlowNode> Switches => Nodes.Where(node => node.Type == UiPathFlowNodeType.Switch).ToArray();

    public bool HasCycles { get; init; }

    public bool HasUnreachableNodes { get; init; }

    public int EntryCount { get; init; }

    public int ExitCount { get; init; }

    public int MergeCount { get; init; }

    public int MaxPathDepth { get; init; }
}

public sealed record UiPathFlowNode
{
    public required string Id { get; init; }

    public UiPathFlowNodeType Type { get; init; } = UiPathFlowNodeType.Activity;

    public string? DisplayName { get; init; }

    public string? ActivityId { get; init; }

    public string? ActivityName { get; init; }

    public bool IsExecutable { get; init; }

    public string? PositionMetadata { get; init; }

    public IReadOnlyDictionary<string, string?> Properties { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}

public sealed record UiPathFlowEdge
{
    public required string SourceNodeId { get; init; }

    public required string TargetNodeId { get; init; }

    public string? Condition { get; init; }

    public string? Label { get; init; }

    public UiPathFlowBranchType BranchType { get; init; } = UiPathFlowBranchType.Unknown;
}

public sealed record UiPathFlowchartConversionAssessment
{
    public required string WorkflowPath { get; init; }

    public bool IsConvertible { get; init; }

    public UiPathFlowchartConversionLevel ConversionLevel { get; init; }

    public UiPathConversionConfidence Confidence { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = [];

    public IReadOnlyList<string> Risks { get; init; } = [];

    public IReadOnlyList<string> RequiredTransformations { get; init; } = [];

    public IReadOnlyList<string> UnsupportedPatterns { get; init; } = [];
}

public sealed record UiPathConversionMapping
{
    public string? SourceActivityLocator { get; init; }

    public required string SourceNodeId { get; init; }

    public required string TargetPath { get; init; }

    public UiPathConversionTransformationType TransformationType { get; init; }
}

public sealed record UiPathSequencePreviewNode
{
    public required string Type { get; init; }

    public string? DisplayName { get; init; }

    public string? SourceNodeId { get; init; }

    public string? Condition { get; init; }

    public IReadOnlyList<UiPathSequencePreviewNode> Children { get; init; } = [];
}

public sealed record UiPathFlowchartConversionPlan
{
    public required string WorkflowPath { get; init; }

    public required UiPathFlowchartConversionAssessment Assessment { get; init; }

    public string ProposedRootType { get; init; } = "Sequence";

    public IReadOnlyList<string> Steps { get; init; } = [];

    public IReadOnlyList<UiPathConversionMapping> Mappings { get; init; } = [];

    public UiPathSequencePreviewNode? PreviewTree { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> ManualReviewItems { get; init; } = [];

    public IReadOnlyList<string> PreservedItems { get; init; } = ["Arguments", "Variables", "Activity properties", "Expressions"];
}

public sealed record UiPathFlowchartWorkflowSummary
{
    public required string WorkflowPath { get; init; }

    public UiPathWorkflowStructureType StructureType { get; init; }

    public bool ContainsFlowchart { get; init; }

    public int FlowchartCount { get; init; }

    public bool IsRootFlowchart { get; init; }

    public int NodeCount { get; init; }

    public int EdgeCount { get; init; }

    public int DecisionCount { get; init; }

    public int SwitchCount { get; init; }

    public bool HasCycles { get; init; }

    public bool HasUnreachableNodes { get; init; }

    public int MergeCount { get; init; }

    public UiPathFlowchartConversionLevel? ConversionLevel { get; init; }

    public UiPathConversionConfidence? Confidence { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = [];
}

public sealed record UiPathFlowchartAnalysisSummary
{
    public int FlowchartWorkflowCount { get; init; }

    public int RootFlowchartWorkflowCount { get; init; }

    public int NestedFlowchartWorkflowCount { get; init; }

    public int TotalFlowchartCount { get; init; }

    public int SequenceWorkflowCount { get; init; }

    public int StateMachineWorkflowCount { get; init; }

    public int MixedWorkflowCount { get; init; }

    public int UnknownWorkflowCount { get; init; }

    public int SafeConversionCount { get; init; }

    public int RequiresReviewCount { get; init; }

    public int ComplexCount { get; init; }

    public int NotSupportedCount { get; init; }

    public IReadOnlyList<UiPathFlowchartWorkflowSummary> Workflows { get; init; } = [];
}

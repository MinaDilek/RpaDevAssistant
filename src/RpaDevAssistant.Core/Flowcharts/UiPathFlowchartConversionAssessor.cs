namespace RpaDevAssistant.Core.Flowcharts;

public static class UiPathFlowchartConversionAssessor
{
    public static UiPathFlowchartConversionAssessment Assess(UiPathFlowchartGraph graph)
    {
        var reasons = new List<string>();
        var risks = new List<string>();
        var transformations = new List<string>();
        var unsupported = new List<string>();

        if (graph.Nodes.Count == 0)
        {
            unsupported.Add("No flow nodes were parsed.");
            return Assessment(graph, false, UiPathFlowchartConversionLevel.NotSupported, UiPathConversionConfidence.Low, reasons, risks, transformations, unsupported);
        }

        if (graph.HasCycles)
        {
            risks.Add("Cycle detected. Flowchart cycles can represent while, retry, or goto-like control flow.");
            transformations.Add("Recognized back-edge loops can be represented as While preview nodes and must be reviewed in UiPath Studio.");
        }

        if (graph.HasUnreachableNodes)
        {
            risks.Add("Unreachable node detected from the Flowchart start node.");
        }

        if (graph.Switches.Count > 0)
        {
            transformations.Add("FlowSwitch nodes can be represented as Switch preview nodes when each case has a clear branch.");
        }

        if (graph.Decisions.Count > 0)
        {
            transformations.Add("FlowDecision nodes can be represented as If preview nodes with True and False branches.");
        }

        foreach (var decision in graph.Decisions)
        {
            var branches = graph.Edges.Where(edge => edge.SourceNodeId.Equals(decision.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (!branches.Any(edge => edge.BranchType == UiPathFlowBranchType.True)
                || !branches.Any(edge => edge.BranchType == UiPathFlowBranchType.False))
            {
                unsupported.Add($"FlowDecision branch targets could not be resolved for node: {decision.Id}.");
            }
        }

        foreach (var flowSwitch in graph.Switches)
        {
            if (!graph.Edges.Any(edge => edge.SourceNodeId.Equals(flowSwitch.Id, StringComparison.OrdinalIgnoreCase)))
            {
                unsupported.Add($"FlowSwitch case targets could not be resolved for node: {flowSwitch.Id}.");
            }
        }

        if (graph.MergeCount > 0)
        {
            transformations.Add("Shared merge points are represented as continuation nodes instead of duplicating branches.");
        }

        var unsupportedNodes = graph.Nodes.Where(node => node.Type == UiPathFlowNodeType.Unknown).ToArray();
        if (unsupportedNodes.Length > 0)
        {
            unsupported.Add($"Unsupported flow node type count: {unsupportedNodes.Length}.");
        }

        if (graph.EntryCount > 1)
        {
            risks.Add($"Multiple entry-like nodes detected: {graph.EntryCount}.");
        }

        if (unsupported.Count > 0)
        {
            return Assessment(graph, false, UiPathFlowchartConversionLevel.NotSupported, UiPathConversionConfidence.Low, reasons, risks, transformations, unsupported);
        }

        if (graph.HasCycles)
        {
            reasons.Add("The Flowchart contains loop/back-edge control flow and requires careful review after conversion.");
            return Assessment(graph, true, UiPathFlowchartConversionLevel.Complex, UiPathConversionConfidence.Low, reasons, risks, transformations, unsupported);
        }

        if (risks.Count > 0 || graph.Decisions.Count > 2 || graph.Switches.Count > 1 || graph.MergeCount > 1)
        {
            reasons.Add("The Flowchart can be previewed, but branch/merge behavior should be reviewed in UiPath Studio.");
            return Assessment(graph, true, UiPathFlowchartConversionLevel.RequiresReview, UiPathConversionConfidence.Medium, reasons, risks, transformations, unsupported);
        }

        reasons.Add(graph.Decisions.Count == 0
            ? "The Flowchart is linear and has no cycles or unreachable nodes."
            : "The Flowchart has simple acyclic decision structure.");

        return Assessment(graph, true, UiPathFlowchartConversionLevel.Safe, UiPathConversionConfidence.High, reasons, risks, transformations, unsupported);
    }

    private static UiPathFlowchartConversionAssessment Assessment(
        UiPathFlowchartGraph graph,
        bool convertible,
        UiPathFlowchartConversionLevel level,
        UiPathConversionConfidence confidence,
        IReadOnlyList<string> reasons,
        IReadOnlyList<string> risks,
        IReadOnlyList<string> transformations,
        IReadOnlyList<string> unsupported)
    {
        return new UiPathFlowchartConversionAssessment
        {
            WorkflowPath = graph.WorkflowPath,
            IsConvertible = convertible,
            ConversionLevel = level,
            Confidence = confidence,
            Reasons = reasons,
            Risks = risks,
            RequiredTransformations = transformations,
            UnsupportedPatterns = unsupported
        };
    }
}

using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Scanning;

namespace RpaDevAssistant.Core.Flowcharts;

public sealed class UiPathFlowchartConversionService : IUiPathFlowchartConversionService
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
    private readonly IUiPathProjectScanner scanner;
    private readonly IUiPathFlowchartAnalyzer analyzer;

    public UiPathFlowchartConversionService(IUiPathProjectScanner scanner, IUiPathFlowchartAnalyzer analyzer)
    {
        this.scanner = scanner;
        this.analyzer = analyzer;
    }

    public Task<UiPathFlowchartConversionResult> AnalyzeAsync(string projectPath, string workflowPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowPath);
        cancellationToken.ThrowIfCancellationRequested();

        var scan = scanner.Scan(projectPath);
        var normalizedWorkflowPath = NormalizeWorkflowPath(workflowPath);
        var workflow = scan.Workflows.FirstOrDefault(item => Comparer.Equals(item.RelativePath, normalizedWorkflowPath));
        if (workflow is null)
        {
            return Task.FromResult(new UiPathFlowchartConversionResult
            {
                WorkflowPath = normalizedWorkflowPath,
                StructureType = UiPathWorkflowStructureType.Unknown,
                Errors = ["Workflow was not found in the selected project."]
            });
        }

        var structure = workflow.Analysis?.StructureType ?? UiPathWorkflowStructureType.Unknown;
        if (structure != UiPathWorkflowStructureType.Flowchart)
        {
            var message = workflow.Analysis?.ContainsFlowchart == true
                ? $"Workflow structure is {structure}; a nested Flowchart was detected, but automatic conversion preview is only available when the workflow root is Flowchart."
                : $"Workflow structure is {structure}; conversion preview is only available for Flowchart workflows.";
            return Task.FromResult(new UiPathFlowchartConversionResult
            {
                WorkflowPath = workflow.RelativePath,
                StructureType = structure,
                Errors = [message]
            });
        }

        var graph = analyzer.AnalyzeGraph(scan.ProjectPath, workflow);
        if (graph is null)
        {
            return Task.FromResult(new UiPathFlowchartConversionResult
            {
                WorkflowPath = workflow.RelativePath,
                StructureType = structure,
                Errors = ["Flowchart graph could not be parsed."]
            });
        }

        var assessment = UiPathFlowchartConversionAssessor.Assess(graph);
        var plan = UiPathFlowchartConversionPlanner.BuildPlan(graph, assessment);
        return Task.FromResult(new UiPathFlowchartConversionResult
        {
            WorkflowPath = workflow.RelativePath,
            StructureType = structure,
            Graph = graph,
            Assessment = assessment,
            Plan = plan,
            WorkflowHash = UiPathFileHash.Sha256(workflow.FullPath)
        });
    }

    private static UiPathFlowchartConversionPlan BuildPlan(UiPathFlowchartGraph graph, UiPathFlowchartConversionAssessment assessment)
    {
        var mappings = new List<UiPathConversionMapping>();
        var preview = new UiPathSequencePreviewNode
        {
            Type = "Sequence",
            DisplayName = Path.GetFileNameWithoutExtension(graph.WorkflowPath),
            Children = BuildPreviewChildren(graph, mappings)
        };

        var warnings = new List<string>();
        warnings.AddRange(assessment.Risks);
        warnings.AddRange(assessment.UnsupportedPatterns);

        var steps = new List<string>
        {
            "Create a Sequence root.",
            "Preserve workflow arguments, variables, activity properties, and expressions.",
            "Move linear FlowStep activities into Sequence order."
        };

        if (graph.Decisions.Count > 0)
        {
            steps.Add("Represent FlowDecision branches as If preview nodes.");
        }

        if (graph.Switches.Count > 0)
        {
            steps.Add("Represent FlowSwitch branches as Switch preview nodes.");
        }

        if (graph.MergeCount > 0)
        {
            steps.Add("Keep shared merge nodes as continuation activities after branch previews.");
        }

        return new UiPathFlowchartConversionPlan
        {
            WorkflowPath = graph.WorkflowPath,
            Assessment = assessment,
            Steps = steps,
            Mappings = mappings,
            PreviewTree = preview,
            Warnings = warnings,
            ManualReviewItems = assessment.ConversionLevel == UiPathFlowchartConversionLevel.Safe
                ? []
                : ["Review branch semantics in UiPath Studio before implementing a real conversion."]
        };
    }

    private static IReadOnlyList<UiPathSequencePreviewNode> BuildPreviewChildren(UiPathFlowchartGraph graph, List<UiPathConversionMapping> mappings)
    {
        var byId = graph.Nodes.ToDictionary(node => node.Id, node => node, Comparer);
        var bySource = graph.Edges.GroupBy(edge => edge.SourceNodeId, Comparer).ToDictionary(group => group.Key, group => group.ToArray(), Comparer);
        var incomingCount = graph.Edges.GroupBy(edge => edge.TargetNodeId, Comparer).ToDictionary(group => group.Key, group => group.Count(), Comparer);
        var emitted = new HashSet<string>(Comparer);
        var output = new List<UiPathSequencePreviewNode>();

        var current = graph.StartNodeId;
        var guard = 0;
        while (!string.IsNullOrWhiteSpace(current) && byId.TryGetValue(current, out var node) && emitted.Add(current) && guard++ < graph.Nodes.Count + 5)
        {
            var targetPath = $"Sequence/{output.Count}";
            var previewNode = BuildPreviewNode(node, graph, byId, bySource, incomingCount, mappings, targetPath);
            output.Add(previewNode);
            foreach (var sourceNodeId in PreviewSourceIds(previewNode))
            {
                emitted.Add(sourceNodeId);
            }
            mappings.Add(new UiPathConversionMapping
            {
                SourceActivityLocator = node.ActivityId,
                SourceNodeId = node.Id,
                TargetPath = targetPath,
                TransformationType = ResolveTransformation(node)
            });

            current = NextContinuation(node, bySource, incomingCount);
        }

        foreach (var unreachable in graph.Nodes.Where(node => !emitted.Contains(node.Id)))
        {
            output.Add(new UiPathSequencePreviewNode
            {
                Type = "Unsupported",
                DisplayName = $"{unreachable.DisplayName ?? unreachable.Id} (unreachable/shared)",
                SourceNodeId = unreachable.Id
            });
            mappings.Add(new UiPathConversionMapping
            {
                SourceActivityLocator = unreachable.ActivityId,
                SourceNodeId = unreachable.Id,
                TargetPath = $"Sequence/{output.Count - 1}",
                TransformationType = UiPathConversionTransformationType.Unsupported
            });
        }

        return output;
    }

    private static UiPathSequencePreviewNode BuildPreviewNode(
        UiPathFlowNode node,
        UiPathFlowchartGraph graph,
        IReadOnlyDictionary<string, UiPathFlowNode> byId,
        IReadOnlyDictionary<string, UiPathFlowEdge[]> bySource,
        IReadOnlyDictionary<string, int> incomingCount,
        List<UiPathConversionMapping> mappings,
        string path)
    {
        if (node.Type == UiPathFlowNodeType.Decision)
        {
            var children = new List<UiPathSequencePreviewNode>();
            foreach (var branch in Branches(node.Id, bySource))
            {
                if (byId.TryGetValue(branch.TargetNodeId, out var target))
                {
                    children.Add(new UiPathSequencePreviewNode
                    {
                        Type = branch.BranchType == UiPathFlowBranchType.True ? "Then" : "Else",
                        DisplayName = branch.Label,
                        SourceNodeId = target.Id,
                        Children = incomingCount.GetValueOrDefault(target.Id) > 1
                            ? []
                            : [BuildPreviewNode(target, graph, byId, bySource, incomingCount, mappings, $"{path}/{branch.BranchType}")]
                    });
                }
            }

            return new UiPathSequencePreviewNode
            {
                Type = "If",
                DisplayName = node.DisplayName,
                SourceNodeId = node.Id,
                Condition = node.Properties.GetValueOrDefault("Condition"),
                Children = children
            };
        }

        if (node.Type == UiPathFlowNodeType.Switch)
        {
            var children = Branches(node.Id, bySource)
                .Where(edge => byId.ContainsKey(edge.TargetNodeId))
                .Select(edge => new UiPathSequencePreviewNode
                {
                    Type = edge.BranchType == UiPathFlowBranchType.Otherwise ? "Default" : "Case",
                    DisplayName = edge.Label,
                    SourceNodeId = edge.TargetNodeId,
                    Children = incomingCount.GetValueOrDefault(edge.TargetNodeId) > 1
                        ? []
                        : [BuildPreviewNode(byId[edge.TargetNodeId], graph, byId, bySource, incomingCount, mappings, $"{path}/{edge.Label ?? edge.BranchType.ToString()}")]
                })
                .ToArray();

            return new UiPathSequencePreviewNode
            {
                Type = "Switch",
                DisplayName = node.DisplayName,
                SourceNodeId = node.Id,
                Condition = node.Properties.GetValueOrDefault("Expression"),
                Children = children
            };
        }

        return new UiPathSequencePreviewNode
        {
            Type = node.ActivityName ?? "Activity",
            DisplayName = node.DisplayName,
            SourceNodeId = node.Id
        };
    }

    private static string? NextContinuation(UiPathFlowNode node, IReadOnlyDictionary<string, UiPathFlowEdge[]> bySource, IReadOnlyDictionary<string, int> incomingCount)
    {
        if (!bySource.TryGetValue(node.Id, out var edges))
        {
            return null;
        }

        var defaultEdge = edges.FirstOrDefault(edge => edge.BranchType == UiPathFlowBranchType.Default);
        if (defaultEdge is not null)
        {
            return defaultEdge.TargetNodeId;
        }

        var branches = edges.Where(edge => edge.BranchType is UiPathFlowBranchType.True or UiPathFlowBranchType.False or UiPathFlowBranchType.Case or UiPathFlowBranchType.Otherwise)
            .Select(edge => edge.TargetNodeId)
            .ToArray();
        if (branches.Length > 1)
        {
            var reachableSets = branches.Select(target => ReachableFrom(target, bySource)).ToArray();
            return reachableSets
                .First()
                .FirstOrDefault(candidate => reachableSets.All(set => set.Contains(candidate)) && incomingCount.GetValueOrDefault(candidate) > 1);
        }

        return null;
    }

    private static IReadOnlyList<string> ReachableFrom(string start, IReadOnlyDictionary<string, UiPathFlowEdge[]> bySource)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        var stack = new Stack<string>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current))
            {
                continue;
            }

            ordered.Add(current);
            if (!bySource.TryGetValue(current, out var next))
            {
                continue;
            }

            foreach (var edge in next.Reverse())
            {
                stack.Push(edge.TargetNodeId);
            }
        }

        return ordered;
    }

    private static IEnumerable<string> PreviewSourceIds(UiPathSequencePreviewNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.SourceNodeId))
        {
            yield return node.SourceNodeId;
        }

        foreach (var child in node.Children.SelectMany(PreviewSourceIds))
        {
            yield return child;
        }
    }

    private static IEnumerable<UiPathFlowEdge> Branches(string nodeId, IReadOnlyDictionary<string, UiPathFlowEdge[]> bySource)
    {
        return bySource.TryGetValue(nodeId, out var edges)
            ? edges.Where(edge => edge.BranchType is UiPathFlowBranchType.True or UiPathFlowBranchType.False or UiPathFlowBranchType.Case or UiPathFlowBranchType.Otherwise)
            : [];
    }

    private static UiPathConversionTransformationType ResolveTransformation(UiPathFlowNode node)
    {
        return node.Type switch
        {
            UiPathFlowNodeType.Decision => UiPathConversionTransformationType.WrappedInIf,
            UiPathFlowNodeType.Switch => UiPathConversionTransformationType.WrappedInSwitch,
            UiPathFlowNodeType.Activity => UiPathConversionTransformationType.MovedToSequence,
            _ => UiPathConversionTransformationType.Unsupported
        };
    }

    private static string NormalizeWorkflowPath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }
}

namespace RpaDevAssistant.Core.Flowcharts;

public static class UiPathFlowchartConversionPlanner
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public static UiPathFlowchartConversionPlan BuildPlan(UiPathFlowchartGraph graph, UiPathFlowchartConversionAssessment assessment, string? projectRoot = null)
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

        if (graph.Nodes.Any(UiPathFlowchartConversionPolicy.IsCommentedCodeBlock))
        {
            steps.Add("Exclude commented-out activity blocks from the generated Sequence.");
        }

        var customDetections = UiPathCustomActivityReplacementResolver.DetectCustomActivities(graph, projectRoot);

        if (customDetections.Count > 0)
        {
            steps.Add($"Detected {customDetections.Count} custom/transitive dependency activity/activities with suggested standard UiPath replacements.");
            foreach (var detection in customDetections.Where(d => d.CustomPackageFamily?.Contains("Transitive", StringComparison.OrdinalIgnoreCase) == true))
            {
                warnings.Add($"Transitive dependency: '{detection.ActivityName}' is used without '{detection.SuggestedPackage}' directly declared in project.json ({detection.CustomPackageFamily}).");
            }
        }


        return new UiPathFlowchartConversionPlan
        {
            WorkflowPath = graph.WorkflowPath,
            Assessment = assessment,
            Steps = steps,
            Mappings = mappings,
            PreviewTree = preview,
            Warnings = warnings,
            CustomActivityDetections = customDetections,
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
            if (UiPathFlowchartConversionPolicy.IsCommentedCodeBlock(node))
            {
                current = NextContinuation(node, bySource, incomingCount);
                continue;
            }

            var targetPath = $"Sequence/{output.Count}";
            var previewNode = BuildPreviewNode(node, byId, bySource, incomingCount, mappings, targetPath);
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

        foreach (var unreachable in graph.Nodes.Where(node =>
                     !emitted.Contains(node.Id)
                     && !UiPathFlowchartConversionPolicy.IsCommentedCodeBlock(node)))
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
        IReadOnlyDictionary<string, UiPathFlowNode> byId,
        IReadOnlyDictionary<string, UiPathFlowEdge[]> bySource,
        IReadOnlyDictionary<string, int> incomingCount,
        List<UiPathConversionMapping> mappings,
        string path)
    {
        if (node.Type == UiPathFlowNodeType.Decision)
        {
            var branches = Branches(node.Id, bySource).ToArray();
            var loopBranch = branches.FirstOrDefault(edge => ReachableFrom(edge.TargetNodeId, bySource).Contains(node.Id, Comparer));
            if (loopBranch is not null)
            {
                var loopCondition = loopBranch.BranchType == UiPathFlowBranchType.False
                    ? $"Not ({node.Properties.GetValueOrDefault("Condition")})"
                    : node.Properties.GetValueOrDefault("Condition");
                var loopTarget = ResolvePreviewTarget(loopBranch.TargetNodeId, byId, bySource, incomingCount);
                var loopBody = loopTarget is null
                    ? []
                    : new[] { BuildPreviewNode(loopTarget, byId, bySource, incomingCount, mappings, $"{path}/While") };

                return new UiPathSequencePreviewNode
                {
                    Type = "While",
                    DisplayName = node.DisplayName,
                    SourceNodeId = node.Id,
                    Condition = loopCondition,
                    Children = loopBody
                };
            }

            var children = new List<UiPathSequencePreviewNode>();
            foreach (var branch in branches)
            {
                var target = ResolvePreviewTarget(branch.TargetNodeId, byId, bySource, incomingCount);
                if (target is not null)
                {
                    children.Add(new UiPathSequencePreviewNode
                    {
                        Type = branch.BranchType == UiPathFlowBranchType.True ? "Then" : "Else",
                        DisplayName = branch.Label,
                        SourceNodeId = target.Id,
                        Children = incomingCount.GetValueOrDefault(target.Id) > 1
                            ? []
                            : [BuildPreviewNode(target, byId, bySource, incomingCount, mappings, $"{path}/{branch.BranchType}")]
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
                .Select(edge => new { Edge = edge, Target = ResolvePreviewTarget(edge.TargetNodeId, byId, bySource, incomingCount) })
                .Where(item => item.Target is not null)
                .Select(item => new UiPathSequencePreviewNode
                {
                    Type = item.Edge.BranchType == UiPathFlowBranchType.Otherwise ? "Default" : "Case",
                    DisplayName = item.Edge.Label,
                    SourceNodeId = item.Target!.Id,
                    Children = incomingCount.GetValueOrDefault(item.Target.Id) > 1
                        ? []
                        : [BuildPreviewNode(item.Target, byId, bySource, incomingCount, mappings, $"{path}/{item.Edge.Label ?? item.Edge.BranchType.ToString()}")]
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

    private static UiPathFlowNode? ResolvePreviewTarget(
        string targetNodeId,
        IReadOnlyDictionary<string, UiPathFlowNode> byId,
        IReadOnlyDictionary<string, UiPathFlowEdge[]> bySource,
        IReadOnlyDictionary<string, int> incomingCount)
    {
        var current = targetNodeId;
        var seen = new HashSet<string>(Comparer);
        while (seen.Add(current) && byId.TryGetValue(current, out var node))
        {
            if (!UiPathFlowchartConversionPolicy.IsCommentedCodeBlock(node))
            {
                return node;
            }

            current = NextContinuation(node, bySource, incomingCount) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(current))
            {
                return null;
            }
        }

        return null;
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
            var loopBranch = edges.FirstOrDefault(edge =>
                edge.BranchType is UiPathFlowBranchType.True or UiPathFlowBranchType.False
                && ReachableFrom(edge.TargetNodeId, bySource).Contains(node.Id, Comparer));
            if (loopBranch is not null)
            {
                return edges
                    .Where(edge => edge.BranchType is UiPathFlowBranchType.True or UiPathFlowBranchType.False)
                    .FirstOrDefault(edge => !Comparer.Equals(edge.TargetNodeId, loopBranch.TargetNodeId))
                    ?.TargetNodeId;
            }

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
}

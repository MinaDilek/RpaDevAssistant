using System.Xml;
using System.Xml.Linq;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Flowcharts;

public sealed class UiPathFlowchartAnalyzer : IUiPathFlowchartAnalyzer
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public UiPathWorkflowStructureType DetectStructure(UiPathWorkflowAnalysis workflow)
    {
        var topLevel = workflow.Activities
            .Where(activity => activity.ParentActivityId is null || activity.Depth == 0)
            .Select(activity => Normalize(activity.Name))
            .Distinct(Comparer)
            .ToArray();

        if (topLevel.Length == 0)
        {
            return UiPathWorkflowStructureType.Unknown;
        }

        var hasSequence = topLevel.Contains("sequence", Comparer);
        var hasFlowchart = topLevel.Contains("flowchart", Comparer);
        var hasStateMachine = topLevel.Any(name => name.Contains("statemachine", StringComparison.OrdinalIgnoreCase));
        var count = new[] { hasSequence, hasFlowchart, hasStateMachine }.Count(Boolean);

        if (count > 1)
        {
            return UiPathWorkflowStructureType.Mixed;
        }

        if (hasSequence)
        {
            return UiPathWorkflowStructureType.Sequence;
        }

        if (hasFlowchart)
        {
            return UiPathWorkflowStructureType.Flowchart;
        }

        if (hasStateMachine)
        {
            return UiPathWorkflowStructureType.StateMachine;
        }

        return UiPathWorkflowStructureType.Unknown;
    }

    public UiPathFlowchartGraph? AnalyzeGraph(string projectPath, UiPathWorkflowInfo workflow)
    {
        if (workflow.Analysis?.ContainsFlowchart != true)
        {
            return null;
        }

        try
        {
            var document = XDocument.Load(workflow.FullPath, LoadOptions.SetLineInfo);
            var flowchart = document.Descendants().FirstOrDefault(element => element.Name.LocalName.Equals("Flowchart", StringComparison.OrdinalIgnoreCase));
            if (flowchart is null)
            {
                return null;
            }

            var rawNodes = ReadNodes(flowchart, workflow.Analysis);
            var edges = ReadEdges(rawNodes);
            var nodes = rawNodes.Select(RemoveInternalProperties).ToArray();
            var startNodeElement = flowchart.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".StartNode", StringComparison.OrdinalIgnoreCase));
            string? startFromElement = null;
            if (startNodeElement is not null)
            {
                var refChild = startNodeElement.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Reference", StringComparison.OrdinalIgnoreCase));
                if (refChild is not null)
                {
                    startFromElement = ResolveReference(ReadAttribute(refChild, "Name")) ?? ResolveReference(refChild.Value);
                }

                startFromElement ??= ResolveReference(startNodeElement.Value);
                startFromElement ??= startNodeElement.Elements().Select(ReadNodeId).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
            }

            var start = ResolveReference(ReadAttribute(flowchart, "StartNode"))
                ?? startFromElement
                ?? nodes.FirstOrDefault()?.Id;
            var reachable = FindReachable(start, edges);
            var hasCycles = HasCycle(start, edges);
            var incoming = edges.GroupBy(edge => edge.TargetNodeId, Comparer).ToDictionary(group => group.Key, group => group.Count(), Comparer);
            var outgoing = edges.GroupBy(edge => edge.SourceNodeId, Comparer).ToDictionary(group => group.Key, group => group.Count(), Comparer);

            return new UiPathFlowchartGraph
            {
                WorkflowPath = workflow.RelativePath,
                StartNodeId = start,
                Nodes = nodes,
                Edges = edges,
                HasCycles = hasCycles,
                HasUnreachableNodes = nodes.Any(node => !reachable.Contains(node.Id)),
                EntryCount = Math.Max(1, nodes.Count(node => !incoming.ContainsKey(node.Id))),
                ExitCount = nodes.Count(node => !outgoing.ContainsKey(node.Id)),
                MergeCount = incoming.Count(pair => pair.Value > 1),
                MaxPathDepth = CalculateMaxPathDepth(start, edges)
            };
        }
        catch (XmlException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public UiPathFlowchartAnalysisSummary AnalyzeProject(ProjectScanResult project)
    {
        var summaries = project.Workflows.Select(workflow =>
        {
            var structure = workflow.Analysis?.StructureType ?? UiPathWorkflowStructureType.Unknown;
            var containsFlowchart = workflow.Analysis?.ContainsFlowchart == true;
            var isRootFlowchart = structure == UiPathWorkflowStructureType.Flowchart;
            var graph = containsFlowchart ? AnalyzeGraph(project.ProjectPath, workflow) : null;
            var assessment = graph is null
                ? null
                : isRootFlowchart
                    ? UiPathFlowchartConversionAssessor.Assess(graph)
                    : NestedFlowchartAssessment(workflow.RelativePath);
            return new UiPathFlowchartWorkflowSummary
            {
                WorkflowPath = workflow.RelativePath,
                StructureType = structure,
                ContainsFlowchart = containsFlowchart,
                FlowchartCount = workflow.Analysis?.FlowchartCount ?? 0,
                IsRootFlowchart = isRootFlowchart,
                NodeCount = graph?.Nodes.Count ?? 0,
                EdgeCount = graph?.Edges.Count ?? 0,
                DecisionCount = graph?.Decisions.Count ?? 0,
                SwitchCount = graph?.Switches.Count ?? 0,
                HasCycles = graph?.HasCycles ?? false,
                HasUnreachableNodes = graph?.HasUnreachableNodes ?? false,
                MergeCount = graph?.MergeCount ?? 0,
                ConversionLevel = assessment?.ConversionLevel,
                Confidence = assessment?.Confidence,
                Reasons = assessment?.Reasons ?? []
            };
        }).ToArray();

        return new UiPathFlowchartAnalysisSummary
        {
            FlowchartWorkflowCount = summaries.Count(item => item.ContainsFlowchart),
            RootFlowchartWorkflowCount = summaries.Count(item => item.IsRootFlowchart),
            NestedFlowchartWorkflowCount = summaries.Count(item => item.ContainsFlowchart && !item.IsRootFlowchart),
            TotalFlowchartCount = summaries.Sum(item => item.FlowchartCount),
            SequenceWorkflowCount = summaries.Count(item => item.StructureType == UiPathWorkflowStructureType.Sequence),
            StateMachineWorkflowCount = summaries.Count(item => item.StructureType == UiPathWorkflowStructureType.StateMachine),
            MixedWorkflowCount = summaries.Count(item => item.StructureType == UiPathWorkflowStructureType.Mixed),
            UnknownWorkflowCount = summaries.Count(item => item.StructureType == UiPathWorkflowStructureType.Unknown),
            SafeConversionCount = summaries.Count(item => item.ConversionLevel == UiPathFlowchartConversionLevel.Safe),
            RequiresReviewCount = summaries.Count(item => item.ConversionLevel == UiPathFlowchartConversionLevel.RequiresReview),
            ComplexCount = summaries.Count(item => item.ConversionLevel == UiPathFlowchartConversionLevel.Complex),
            NotSupportedCount = summaries.Count(item => item.ConversionLevel == UiPathFlowchartConversionLevel.NotSupported),
            Workflows = summaries
        };
    }

    private static UiPathFlowchartConversionAssessment NestedFlowchartAssessment(string workflowPath)
    {
        return new UiPathFlowchartConversionAssessment
        {
            WorkflowPath = workflowPath,
            IsConvertible = false,
            ConversionLevel = UiPathFlowchartConversionLevel.NotSupported,
            Confidence = UiPathConversionConfidence.High,
            Reasons = ["Nested Flowchart detected inside a non-Flowchart workflow."],
            UnsupportedPatterns = ["Automatic conversion currently supports only workflows whose root activity is Flowchart."]
        };
    }

    private static IReadOnlyList<UiPathFlowNode> ReadNodes(XElement flowchart, UiPathWorkflowAnalysis workflow)
    {
        var activitiesByPath = workflow.Activities
            .Where(activity => !string.IsNullOrWhiteSpace(activity.ActivityPath))
            .ToDictionary(activity => activity.ActivityPath!, activity => activity, Comparer);

        return flowchart.Descendants()
            .Where(IsFlowNodeElement)
            .Select((element, index) =>
            {
                var id = ReadNodeId(element) ?? $"node-{index}";
                var childActivity = FindFirstChildActivity(element, activitiesByPath);
                var type = ResolveNodeType(element);
                var firstElementName = FindFirstActivityElementName(element);
                var isCommented = (childActivity is not null && UiPathFlowchartConversionPolicy.IsCommentedCodeBlock(childActivity.Name))
                    || UiPathFlowchartConversionPolicy.IsCommentedCodeBlock(firstElementName);
                var displayName = isCommented
                    ? (childActivity?.DisplayName ?? firstElementName ?? "CommentOut")
                    : (ReadAttribute(element, "DisplayName") ?? childActivity?.DisplayName ?? firstElementName ?? id);
                var activityName = isCommented
                    ? (childActivity?.Name ?? firstElementName ?? "CommentOut")
                    : (childActivity?.Name ?? firstElementName ?? (type == UiPathFlowNodeType.Decision ? "FlowDecision" : type == UiPathFlowNodeType.Switch ? "FlowSwitch" : null));
                return new UiPathFlowNode
                {
                    Id = id,
                    Type = type,
                    DisplayName = displayName,
                    ActivityId = childActivity?.ActivityId,
                    ActivityName = activityName,
                    IsExecutable = !isCommented && childActivity is not null && UiPathActivityClassifier.IsExecutable(childActivity),
                    Properties = ReadProperties(element, childActivity),
                    PositionMetadata = TryReadLineInfo(element)
                };
            })
            .DistinctBy(node => node.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static readonly HashSet<string> NonActivityContainerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ActivityAction",
        "DelegateInArgument",
        "Dictionary",
        "InArgument",
        "InOutArgument",
        "OutArgument",
        "Variable",
        "Target",
        "Point",
        "Size",
        "PointCollection",
        "Collection",
        "Array"
    };

    private static string? FindFirstActivityElementName(XElement element)
    {
        return element.Elements()
            .Where(d => !IsFlowNodeElement(d)
                && !d.Name.LocalName.Contains('.', StringComparison.Ordinal)
                && !NonActivityContainerNames.Contains(d.Name.LocalName))
            .Select(d => d.Name.LocalName)
            .FirstOrDefault()
            ?? element.Descendants()
                .Where(d => !IsFlowNodeElement(d)
                    && !d.Name.LocalName.Contains('.', StringComparison.Ordinal)
                    && !NonActivityContainerNames.Contains(d.Name.LocalName))
                .Select(d => d.Name.LocalName)
                .FirstOrDefault();
    }


    private static IReadOnlyList<UiPathFlowEdge> ReadEdges(IReadOnlyList<UiPathFlowNode> nodes)
    {
        return nodes
            .SelectMany(node => ReadEdgesFromNode(node))
            .Where(edge => nodes.Any(node => Comparer.Equals(node.Id, edge.TargetNodeId)))
            .DistinctBy(edge => $"{edge.SourceNodeId}|{edge.TargetNodeId}|{edge.BranchType}|{edge.Label}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<UiPathFlowEdge> ReadEdgesFromNode(UiPathFlowNode node)
    {
        if (!node.Properties.TryGetValue("__element", out var marker) || marker is null)
        {
            yield break;
        }

        var element = XDocument.Parse(marker).Root;
        if (element is null)
        {
            yield break;
        }

        foreach (var next in TargetsFromWrapper(element, "Next"))
        {
            yield return new UiPathFlowEdge { SourceNodeId = node.Id, TargetNodeId = next, BranchType = UiPathFlowBranchType.Default };
        }

        foreach (var target in TargetsFromWrapper(element, "True"))
        {
            yield return new UiPathFlowEdge { SourceNodeId = node.Id, TargetNodeId = target, BranchType = UiPathFlowBranchType.True, Condition = node.Properties.GetValueOrDefault("Condition"), Label = "True" };
        }

        foreach (var target in TargetsFromWrapper(element, "False"))
        {
            yield return new UiPathFlowEdge { SourceNodeId = node.Id, TargetNodeId = target, BranchType = UiPathFlowBranchType.False, Condition = node.Properties.GetValueOrDefault("Condition"), Label = "False" };
        }

        foreach (var target in TargetsFromWrapper(element, "Default"))
        {
            yield return new UiPathFlowEdge { SourceNodeId = node.Id, TargetNodeId = target, BranchType = UiPathFlowBranchType.Otherwise, Label = "Default" };
        }

        foreach (var @case in element.Descendants().Where(item => item.Name.LocalName.EndsWith(".Case", StringComparison.OrdinalIgnoreCase)))
        {
            var label = ReadAttribute(@case, "Key") ?? ReadAttribute(@case, "x:Key") ?? ReadAttribute(@case, "Value");
            foreach (var target in @case.Elements().Select(ReadNodeId).Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                yield return new UiPathFlowEdge { SourceNodeId = node.Id, TargetNodeId = target!, BranchType = UiPathFlowBranchType.Case, Label = label };
            }
        }
    }

    private static IEnumerable<string> TargetsFromWrapper(XElement element, string wrapperName)
    {
        foreach (var wrapper in element.Elements().Where(child => child.Name.LocalName.EndsWith("." + wrapperName, StringComparison.OrdinalIgnoreCase)))
        {
            var reference = ReadAttribute(wrapper, "Reference") ?? wrapper.Value;
            var resolved = ResolveReference(reference);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                yield return resolved;
            }

            foreach (var child in wrapper.Elements())
            {
                var childId = child.Name.LocalName.Equals("Reference", StringComparison.OrdinalIgnoreCase)
                    ? ReadAttribute(child, "Name")
                    : ReadNodeId(child);
                if (!string.IsNullOrWhiteSpace(childId))
                {
                    yield return childId;
                }
            }
        }
    }

    private static UiPathActivityInfo? FindFirstChildActivity(XElement nodeElement, IReadOnlyDictionary<string, UiPathActivityInfo> activitiesByPath)
    {
        var directChild = nodeElement.Elements()
            .FirstOrDefault(d => !IsFlowNodeElement(d)
                && !d.Name.LocalName.Contains('.', StringComparison.Ordinal)
                && !NonActivityContainerNames.Contains(d.Name.LocalName));

        if (directChild is not null && UiPathFlowchartConversionPolicy.IsCommentedCodeBlock(directChild.Name.LocalName))
        {
            var commentId = ReadAttribute(directChild, "WorkflowViewState.IdRef") ?? ReadAttribute(directChild, "IdRef");
            if (!string.IsNullOrWhiteSpace(commentId))
            {
                var match = activitiesByPath.Values.FirstOrDefault(activity => Comparer.Equals(activity.ActivityId, commentId) || Comparer.Equals(activity.StableId, commentId));
                if (match is not null)
                {
                    return match;
                }
            }

            return new UiPathActivityInfo
            {
                ActivityId = commentId ?? "commentout",
                Name = directChild.Name.LocalName,
                DisplayName = ReadAttribute(directChild, "DisplayName") ?? directChild.Name.LocalName,
                TypeName = directChild.Name.LocalName,
                XamlFile = string.Empty
            };
        }

        var candidates = nodeElement.Elements()
            .Where(d => !IsFlowNodeElement(d)
                && !d.Name.LocalName.Contains('.', StringComparison.Ordinal)
                && !NonActivityContainerNames.Contains(d.Name.LocalName))
            .Concat(nodeElement.Descendants()
                .Where(d => !IsFlowNodeElement(d)
                    && !d.Name.LocalName.Contains('.', StringComparison.Ordinal)
                    && !NonActivityContainerNames.Contains(d.Name.LocalName)
                    && !d.Ancestors().Any(a => UiPathFlowchartConversionPolicy.IsCommentedCodeBlock(a.Name.LocalName))));

        foreach (var descendant in candidates)
        {
            var id = ReadAttribute(descendant, "WorkflowViewState.IdRef") ?? ReadAttribute(descendant, "IdRef");
            if (!string.IsNullOrWhiteSpace(id))
            {
                var match = activitiesByPath.Values.FirstOrDefault(activity => Comparer.Equals(activity.ActivityId, id) || Comparer.Equals(activity.StableId, id));
                if (match is not null)
                {
                    return match;
                }
            }

            var local = descendant.Name.LocalName.Replace("_", string.Empty, StringComparison.Ordinal);
            var display = ReadAttribute(descendant, "DisplayName") ?? local;
            var candidate = activitiesByPath.Values.FirstOrDefault(activity => Comparer.Equals(activity.Name, local) && Comparer.Equals(activity.DisplayName, display));
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string?> ReadProperties(XElement element, UiPathActivityInfo? activity)
    {
        var properties = new Dictionary<string, string?>(Comparer)
        {
            ["__element"] = element.ToString(SaveOptions.DisableFormatting)
        };

        var directActivity = element.Elements()
            .FirstOrDefault(d => !IsFlowNodeElement(d)
                && !d.Name.LocalName.Contains('.', StringComparison.Ordinal)
                && !NonActivityContainerNames.Contains(d.Name.LocalName))
            ?? element.Descendants()
                .FirstOrDefault(d => !IsFlowNodeElement(d)
                    && !d.Name.LocalName.Contains('.', StringComparison.Ordinal)
                    && !NonActivityContainerNames.Contains(d.Name.LocalName));

        if (directActivity is not null && !string.IsNullOrWhiteSpace(directActivity.Name.NamespaceName))
        {
            properties["__namespace"] = directActivity.Name.NamespaceName;
        }

        foreach (var attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
        {
            properties[attribute.Name.LocalName] = attribute.Value;
        }

        if (directActivity is not null)
        {
            foreach (var attribute in directActivity.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
            {
                properties.TryAdd(attribute.Name.LocalName, attribute.Value);
            }
        }

        if (activity is not null)
        {
            foreach (var pair in activity.Properties)
            {
                properties.TryAdd(pair.Key, pair.Value);
            }
        }

        return properties;
    }


    private static UiPathFlowNode RemoveInternalProperties(UiPathFlowNode node)
    {
        return node with
        {
            Properties = node.Properties
                .Where(pair => !pair.Key.StartsWith("__", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, Comparer)
        };
    }

    private static UiPathFlowNodeType ResolveNodeType(XElement element)
    {
        var name = element.Name.LocalName;
        if (name.Equals("FlowDecision", StringComparison.OrdinalIgnoreCase))
        {
            return UiPathFlowNodeType.Decision;
        }

        if (name.StartsWith("FlowSwitch", StringComparison.OrdinalIgnoreCase))
        {
            return UiPathFlowNodeType.Switch;
        }

        if (name.Equals("FlowStep", StringComparison.OrdinalIgnoreCase))
        {
            return UiPathFlowNodeType.Activity;
        }

        return UiPathFlowNodeType.Unknown;
    }

    private static bool IsFlowNodeElement(XElement element)
    {
        return element.Name.LocalName.Equals("FlowStep", StringComparison.OrdinalIgnoreCase)
            || element.Name.LocalName.Equals("FlowDecision", StringComparison.OrdinalIgnoreCase)
            || (element.Name.LocalName.StartsWith("FlowSwitch", StringComparison.OrdinalIgnoreCase)
                && !element.Name.LocalName.Contains('.', StringComparison.Ordinal));
    }

    private static string? ReadNodeId(XElement element)
    {
        return ReadAttribute(element, "Name")
            ?? ReadAttribute(element, "Key")
            ?? ReadAttribute(element, "WorkflowViewState.IdRef")
            ?? ReadAttribute(element, "IdRef");
    }

    private static string? ReadAttribute(XElement element, string localName)
    {
        return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static string? ResolveReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.StartsWith("{x:Reference ", StringComparison.OrdinalIgnoreCase) && text.EndsWith('}'))
        {
            return text["{x:Reference ".Length..^1].Trim();
        }

        if (text.StartsWith("{Reference ", StringComparison.OrdinalIgnoreCase) && text.EndsWith('}'))
        {
            return text["{Reference ".Length..^1].Trim();
        }

        return text;
    }

    private static HashSet<string> FindReachable(string? start, IReadOnlyList<UiPathFlowEdge> edges)
    {
        var reachable = new HashSet<string>(Comparer);
        if (string.IsNullOrWhiteSpace(start))
        {
            return reachable;
        }

        var bySource = edges.GroupBy(edge => edge.SourceNodeId, Comparer).ToDictionary(group => group.Key, group => group.ToArray(), Comparer);
        var stack = new Stack<string>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!reachable.Add(current) || !bySource.TryGetValue(current, out var next))
            {
                continue;
            }

            foreach (var edge in next)
            {
                stack.Push(edge.TargetNodeId);
            }
        }

        return reachable;
    }

    private static bool HasCycle(string? start, IReadOnlyList<UiPathFlowEdge> edges)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            return false;
        }

        var bySource = edges.GroupBy(edge => edge.SourceNodeId, Comparer).ToDictionary(group => group.Key, group => group.Select(edge => edge.TargetNodeId).ToArray(), Comparer);
        var visiting = new HashSet<string>(Comparer);
        var visited = new HashSet<string>(Comparer);

        bool Visit(string node)
        {
            if (visiting.Contains(node))
            {
                return true;
            }

            if (!visited.Add(node))
            {
                return false;
            }

            visiting.Add(node);
            if (bySource.TryGetValue(node, out var targets) && targets.Any(Visit))
            {
                return true;
            }

            visiting.Remove(node);
            return false;
        }

        return Visit(start);
    }

    private static int CalculateMaxPathDepth(string? start, IReadOnlyList<UiPathFlowEdge> edges)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            return 0;
        }

        var bySource = edges.GroupBy(edge => edge.SourceNodeId, Comparer).ToDictionary(group => group.Key, group => group.Select(edge => edge.TargetNodeId).ToArray(), Comparer);
        var best = 0;
        void Walk(string node, int depth, HashSet<string> path)
        {
            best = Math.Max(best, depth);
            if (!path.Add(node) || !bySource.TryGetValue(node, out var targets))
            {
                return;
            }

            foreach (var target in targets)
            {
                Walk(target, depth + 1, new HashSet<string>(path, Comparer));
            }
        }

        Walk(start, 1, new HashSet<string>(Comparer));
        return best;
    }

    private static string? TryReadLineInfo(XElement element)
    {
        return element is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? $"line {lineInfo.LineNumber}"
            : null;
    }

    private static string Normalize(string value) => value.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static bool Boolean(bool value) => value;
}

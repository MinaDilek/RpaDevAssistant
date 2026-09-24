using System.Xml;
using System.Xml.Linq;
using RpaDevAssistant.Core.Flowcharts;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Parsing;

public sealed class UiPathXamlParser : IUiPathXamlParser
{
    private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

    private static readonly HashSet<string> IgnoredAttributeNames = new(KeyComparer)
    {
        "Class",
        "Ignorable",
        "WorkflowViewState.IdRef",
        "VisualBasic.Settings",
        "VirtualizedContainerService.HintSize",
        "ViewStateManager.ViewStateManager"
    };

    private static readonly HashSet<string> IgnoredElementNames = new(KeyComparer)
    {
        "Members",
        "Property",
        "References",
        "Null",
        "Array",
        "VisualBasic.Settings",
        "WorkflowViewStateService.ViewState",
        "ViewStateManager"
    };

    private static readonly HashSet<string> NonActivityContainerNames = new(KeyComparer)
    {
        "ActivityAction",
        "DelegateInArgument",
        "Dictionary",
        "InArgument",
        "InOutArgument",
        "OutArgument",
        "Variable",
        "Target"
    };

    private static readonly HashSet<string> IgnoredNamespaceFragments = new(KeyComparer)
    {
        "schemas.microsoft.com/winfx/2006/xaml",
        "schemas.openxmlformats.org/markup-compatibility/2006",
        "schemas.microsoft.com/netfx/2009/xaml/activities/presentation",
        "schemas.microsoft.com/netfx/2010/xaml/activities/presentation"
    };

    public UiPathWorkflowAnalysis Parse(string xamlPath, string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xamlPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var fullPath = Path.GetFullPath(xamlPath);
        var normalizedProjectRoot = Path.GetFullPath(projectRoot);
        var relativePath = NormalizeRelativePath(Path.GetRelativePath(normalizedProjectRoot, fullPath));
        var analysis = new UiPathWorkflowAnalysis
        {
            FileName = Path.GetFileName(fullPath),
            RelativePath = relativePath
        };

        if (!File.Exists(fullPath))
        {
            analysis.ParseErrors.Add("XAML file was not found.");
            return analysis;
        }

        try
        {
            var document = XDocument.Load(fullPath, LoadOptions.SetLineInfo);
            if (document.Root is null)
            {
                analysis.ParseErrors.Add("XAML document does not have a root element.");
                return analysis;
            }

            ReadArguments(document, analysis);
            ReadVariables(document, analysis);

            if (IsWorkflowRootWrapper(document.Root))
            {
                var rootChildIndex = 0;
                foreach (var child in document.Root.Elements())
                {
                    ParseActivities(child, analysis, parentActivityId: null, depth: 0, activityPath: rootChildIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), forceDescendantScan: true);
                    rootChildIndex++;
                }
            }
            else
            {
                ParseActivities(document.Root, analysis, parentActivityId: null, depth: 0, activityPath: "0", forceDescendantScan: true);
            }

            analysis.StructureType = DetectStructureType(analysis);
            analysis.ContainsFlowchart = analysis.Activities.Any(activity => activity.Name.Equals("Flowchart", StringComparison.OrdinalIgnoreCase));
            analysis.FlowchartCount = analysis.Activities.Count(activity => activity.Name.Equals("Flowchart", StringComparison.OrdinalIgnoreCase));
            analysis.ContainsStateMachine = analysis.Activities.Any(activity => activity.Name.Contains("StateMachine", StringComparison.OrdinalIgnoreCase));
        }
        catch (XmlException ex)
        {
            analysis.ParseErrors.Add($"Invalid XML at line {ex.LineNumber}, position {ex.LinePosition}: {ex.Message}");
        }
        catch (IOException ex)
        {
            analysis.ParseErrors.Add($"XAML file could not be read: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            analysis.ParseErrors.Add($"XAML file could not be accessed: {ex.Message}");
        }

        return analysis;
    }

    private static void ReadArguments(XDocument document, UiPathWorkflowAnalysis analysis)
    {
        foreach (var property in document.Descendants().Where(IsArgumentPropertyElement))
        {
            var name = ReadAttributeValue(property, "Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var type = ReadAttributeValue(property, "Type");
            var defaultValue = ReadArgumentDefaultValue(document, property, name);
            analysis.Arguments.Add(new UiPathArgumentInfo
            {
                Name = name,
                Type = type,
                Direction = DetectArgumentDirection(name, type),
                DefaultValue = defaultValue.Value,
                HasDefaultValue = defaultValue.HasValue
            });
        }
    }

    private static (bool HasValue, string? Value) ReadArgumentDefaultValue(
        XDocument document,
        XElement property,
        string argumentName)
    {
        var defaultAttribute = property.Attributes().FirstOrDefault(attribute =>
            attribute.Name.LocalName.Equals("Default", StringComparison.OrdinalIgnoreCase));
        if (defaultAttribute is not null)
        {
            return (true, defaultAttribute.Value);
        }

        var defaultElement = property.Elements().FirstOrDefault(element =>
            element.Name.LocalName.EndsWith(".Default", StringComparison.OrdinalIgnoreCase));
        if (defaultElement is not null)
        {
            return (true, ReadSerializedExpression(defaultElement));
        }

        var rootBinding = document.Root?.Elements().FirstOrDefault(element =>
            element.Name.LocalName.EndsWith($".{argumentName}", StringComparison.OrdinalIgnoreCase));
        if (rootBinding is null)
        {
            return (false, null);
        }

        var argument = rootBinding.DescendantsAndSelf().FirstOrDefault(element =>
            element.Name.LocalName.Equals("InArgument", StringComparison.OrdinalIgnoreCase)
            || element.Name.LocalName.Equals("InOutArgument", StringComparison.OrdinalIgnoreCase));
        if (argument is null || (!argument.HasElements && string.IsNullOrWhiteSpace(argument.Value)))
        {
            return (false, null);
        }

        return (true, ReadSerializedExpression(argument));
    }

    private static string? ReadSerializedExpression(XElement element)
    {
        var expressionAttribute = element.DescendantsAndSelf()
            .SelectMany(candidate => candidate.Attributes())
            .FirstOrDefault(attribute =>
                attribute.Name.LocalName.Equals("ExpressionText", StringComparison.OrdinalIgnoreCase)
                || attribute.Name.LocalName.Equals("Value", StringComparison.OrdinalIgnoreCase));
        if (expressionAttribute is not null)
        {
            return expressionAttribute.Value;
        }

        if (element.DescendantsAndSelf().Any(candidate =>
                candidate.Name.LocalName.Equals("Null", StringComparison.OrdinalIgnoreCase)))
        {
            return "{x:Null}";
        }

        var value = element.Value.Trim();
        return value.Length == 0 ? null : value;
    }

    private static bool IsArgumentPropertyElement(XElement element)
    {
        return element.Name.LocalName.Equals("Property", StringComparison.OrdinalIgnoreCase)
            && element.Parent?.Name.LocalName.Equals("Members", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void ReadVariables(XDocument document, UiPathWorkflowAnalysis analysis)
    {
        foreach (var element in document.Descendants().Where(candidate =>
                     candidate.Name.LocalName.Equals("Variable", StringComparison.OrdinalIgnoreCase)))
        {
            var name = ReadAttributeValue(element, "Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var defaultWrapper = element.Elements().FirstOrDefault(child =>
                child.Name.LocalName.EndsWith(".Default", StringComparison.OrdinalIgnoreCase));
            var scopeElement = element.Parent?.Parent;
            analysis.Variables.Add(new UiPathVariableInfo
            {
                Name = name,
                Type = ReadAttributeValue(element, "TypeArguments") ?? ReadAttributeValue(element, "Type"),
                DefaultValue = ReadAttributeValue(element, "Default") ?? defaultWrapper?.Value.Trim(),
                Scope = scopeElement is null
                    ? null
                    : ReadAttributeValue(scopeElement, "DisplayName") ?? scopeElement.Name.LocalName,
                ScopeActivityId = scopeElement is null
                    ? null
                    : ReadAttributeValue(scopeElement, "WorkflowViewState.IdRef") ?? ReadAttributeValue(scopeElement, "IdRef")
            });
        }
    }

    private static bool IsWorkflowRootWrapper(XElement element)
    {
        return element.Name.LocalName.Equals("Activity", StringComparison.OrdinalIgnoreCase)
            && ReadAttributeValue(element, "Class") is not null;
    }

    private static string? DetectArgumentDirection(string name, string? type)
    {
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (type.StartsWith("InOutArgument", StringComparison.OrdinalIgnoreCase))
            {
                return "InOut";
            }

            if (type.StartsWith("InArgument", StringComparison.OrdinalIgnoreCase))
            {
                return "In";
            }

            if (type.StartsWith("OutArgument", StringComparison.OrdinalIgnoreCase))
            {
                return "Out";
            }
        }

        if (name.StartsWith("io_", StringComparison.OrdinalIgnoreCase))
        {
            return "InOut";
        }

        if (name.StartsWith("in_", StringComparison.OrdinalIgnoreCase))
        {
            return "In";
        }

        if (name.StartsWith("out_", StringComparison.OrdinalIgnoreCase))
        {
            return "Out";
        }

        return null;
    }

    private static void ParseActivities(
        XElement element,
        UiPathWorkflowAnalysis analysis,
        string? parentActivityId,
        int depth,
        string activityPath,
        bool forceDescendantScan = false)
    {
        if (IsIgnoredMetadataElement(element))
        {
            return;
        }

        if (IsActivityElement(element))
        {
            var stableId = ReadAttributeValue(element, "WorkflowViewState.IdRef") ?? ReadAttributeValue(element, "IdRef");
            var activityId = string.IsNullOrWhiteSpace(stableId) ? activityPath : stableId;
            var name = NormalizeActivityName(element.Name.LocalName);
            var argumentMappings = ReadActivityArguments(element);
            analysis.Activities.Add(new UiPathActivityInfo
            {
                ActivityId = activityId,
                ParentActivityId = parentActivityId,
                StableId = stableId,
                ActivityPath = activityPath,
                Name = name,
                DisplayName = ReadAttributeValue(element, "DisplayName") ?? name,
                TypeName = element.Name.LocalName,
                Namespace = string.IsNullOrWhiteSpace(element.Name.NamespaceName) ? null : element.Name.NamespaceName,
                Depth = depth,
                XamlFile = analysis.RelativePath,
                Properties = ReadProperties(element),
                Arguments = argumentMappings.Values,
                ArgumentMappingDirections = argumentMappings.Directions
            });

            var childIndex = 0;
            foreach (var child in element.Elements())
            {
                ParseActivities(child, analysis, activityId, depth + 1, $"{activityPath}/{childIndex}");
                childIndex++;
            }

            return;
        }

        if (forceDescendantScan || IsContainerElement(element))
        {
            var childIndex = 0;
            foreach (var child in element.Elements())
            {
                ParseActivities(child, analysis, parentActivityId, depth, $"{activityPath}/{childIndex}");
                childIndex++;
            }
        }
    }

    private static bool IsActivityElement(XElement element)
    {
        var localName = element.Name.LocalName;
        if (string.IsNullOrWhiteSpace(localName)
            || localName.Contains('.', StringComparison.Ordinal)
            || IgnoredElementNames.Contains(localName)
            || NonActivityContainerNames.Contains(localName)
            || IsIgnoredNamespace(element.Name.NamespaceName))
        {
            return false;
        }

        return true;
    }

    private static bool IsContainerElement(XElement element)
    {
        return element.Name.LocalName.Contains('.', StringComparison.Ordinal)
            || element.HasElements;
    }

    private static bool IsIgnoredMetadataElement(XElement element)
    {
        return IgnoredElementNames.Contains(element.Name.LocalName)
            && !element.Name.LocalName.Equals("Array", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnoredNamespace(string namespaceName)
    {
        return IgnoredNamespaceFragments.Any(fragment => namespaceName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<string, string?> ReadProperties(XElement element)
    {
        var properties = new Dictionary<string, string?>(KeyComparer);

        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration || ShouldIgnoreAttribute(attribute))
            {
                continue;
            }

            properties[NormalizePropertyName(attribute.Name.LocalName)] = attribute.Value;
        }

        foreach (var wrapper in element.Elements().Where(IsPropertyWrapperElement))
        {
            var wrapperPropertyName = NormalizePropertyName(wrapper.Name.LocalName);
            var wrapperValue = ReadPropertyWrapperValue(wrapper);
            if (!string.IsNullOrWhiteSpace(wrapperValue))
            {
                properties.TryAdd(wrapperPropertyName, wrapperValue);
            }

            foreach (var nestedElement in wrapper.Elements())
            {
                if (nestedElement.Name.LocalName.Equals("Variable", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var attribute in nestedElement.Attributes())
                {
                    if (attribute.IsNamespaceDeclaration || ShouldIgnoreAttribute(attribute))
                    {
                        continue;
                    }

                    properties.TryAdd(NormalizePropertyName(attribute.Name.LocalName), attribute.Value);
                }
            }
        }

        return properties;
    }

    private static ActivityArgumentMappings ReadActivityArguments(XElement element)
    {
        var arguments = new Dictionary<string, string?>(KeyComparer);
        var directions = new Dictionary<string, string?>(KeyComparer);
        if (!NormalizeActivityName(element.Name.LocalName).Equals("InvokeWorkflowFile", StringComparison.OrdinalIgnoreCase))
        {
            return new ActivityArgumentMappings(arguments, directions);
        }

        foreach (var wrapper in element.Elements().Where(child =>
                     child.Name.LocalName.EndsWith(".Arguments", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var mapping in wrapper.Descendants())
            {
                var key = mapping.Attributes().FirstOrDefault(attribute =>
                    attribute.Name.LocalName.Equals("Key", StringComparison.OrdinalIgnoreCase))?.Value;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var value = mapping.Value.Trim();
                arguments[key] = string.IsNullOrWhiteSpace(value) ? null : value;
                directions[key] = NormalizeArgumentDirection(mapping.Name.LocalName);
            }
        }

        return new ActivityArgumentMappings(arguments, directions);
    }

    private static string? NormalizeArgumentDirection(string localName) => localName switch
    {
        "InArgument" => "In",
        "OutArgument" => "Out",
        "InOutArgument" => "InOut",
        _ => null
    };

    private sealed record ActivityArgumentMappings(
        IReadOnlyDictionary<string, string?> Values,
        IReadOnlyDictionary<string, string?> Directions);

    private static string? ReadPropertyWrapperValue(XElement wrapper)
    {
        var valueElement = wrapper.Elements().FirstOrDefault(element => !element.HasElements);
        var value = valueElement?.Value ?? (!wrapper.HasElements ? wrapper.Value : null);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsPropertyWrapperElement(XElement element)
    {
        return element.Name.LocalName.Contains('.', StringComparison.Ordinal);
    }

    private static bool ShouldIgnoreAttribute(XAttribute attribute)
    {
        if (attribute.Name.LocalName.Equals("TypeArguments", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IgnoredAttributeNames.Contains(attribute.Name.LocalName)
            || IsIgnoredNamespace(attribute.Name.NamespaceName);
    }

    private static string NormalizePropertyName(string propertyName)
    {
        var dotIndex = propertyName.LastIndexOf('.');
        return dotIndex >= 0 && dotIndex < propertyName.Length - 1
            ? propertyName[(dotIndex + 1)..]
            : propertyName;
    }

    private static string NormalizeActivityName(string localName)
    {
        return localName.Replace("_", string.Empty, StringComparison.Ordinal);
    }

    private static string? ReadAttributeValue(XElement element, string localName)
    {
        return element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? "."
            : relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static UiPathWorkflowStructureType DetectStructureType(UiPathWorkflowAnalysis analysis)
    {
        var topLevel = analysis.Activities
            .Where(activity => activity.ParentActivityId is null || activity.Depth == 0)
            .Select(activity => NormalizeActivityName(activity.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (topLevel.Length == 0)
        {
            return UiPathWorkflowStructureType.Unknown;
        }

        var hasSequence = topLevel.Any(name => name.Equals("Sequence", StringComparison.OrdinalIgnoreCase));
        var hasFlowchart = topLevel.Any(name => name.Equals("Flowchart", StringComparison.OrdinalIgnoreCase));
        var hasStateMachine = topLevel.Any(name => name.Contains("StateMachine", StringComparison.OrdinalIgnoreCase));
        var structuralCount = new[] { hasSequence, hasFlowchart, hasStateMachine }.Count(value => value);

        if (structuralCount > 1)
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
}

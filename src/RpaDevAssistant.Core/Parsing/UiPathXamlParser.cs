using System.Xml;
using System.Xml.Linq;
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
        "InArgument",
        "InOutArgument",
        "OutArgument",
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
            analysis.Arguments.Add(new UiPathArgumentInfo
            {
                Name = name,
                Type = type,
                Direction = DetectArgumentDirection(name, type)
            });
        }
    }

    private static bool IsArgumentPropertyElement(XElement element)
    {
        return element.Name.LocalName.Equals("Property", StringComparison.OrdinalIgnoreCase)
            && element.Parent?.Name.LocalName.Equals("Members", StringComparison.OrdinalIgnoreCase) == true;
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
                Properties = ReadProperties(element)
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
            foreach (var nestedElement in wrapper.Elements())
            {
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

    private static bool IsPropertyWrapperElement(XElement element)
    {
        return element.Name.LocalName.Contains('.', StringComparison.Ordinal);
    }

    private static bool ShouldIgnoreAttribute(XAttribute attribute)
    {
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
}

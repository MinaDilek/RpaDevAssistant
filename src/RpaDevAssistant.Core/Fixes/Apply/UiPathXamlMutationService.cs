using System.Xml;
using System.Xml.Linq;

namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed class UiPathXamlMutationService : IUiPathXamlMutationService
{
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    public UiPathXamlMutationResult BuildMutation(UiPathXamlMutationRequest request)
    {
        if (!File.Exists(request.WorkflowFullPath))
        {
            return Failed("Workflow file was not found.", "workflow_missing");
        }

        XDocument document;
        try
        {
            document = XDocument.Load(request.WorkflowFullPath, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            return Failed($"XAML could not be parsed: {ex.Message}", "invalid_xaml");
        }
        catch (IOException ex)
        {
            return Failed($"Workflow file could not be read: {ex.Message}", "file_read_failed");
        }

        var elementResult = FindElement(document, request.Locator, request.ExpectedCurrentValue);
        if (!elementResult.Success || elementResult.Element is null)
        {
            return Failed(elementResult.Message, elementResult.ErrorCode);
        }

        var attribute = FindAttribute(elementResult.Element, request.PropertyName);
        var currentValue = attribute?.Value;
        if (!string.Equals(currentValue, request.ExpectedCurrentValue, StringComparison.Ordinal))
        {
            return Failed("Fix is stale because the activity has changed since the suggestion was generated.", "stale_current_value");
        }

        if (string.IsNullOrWhiteSpace(request.SuggestedValue))
        {
            return Failed("Suggested value cannot be empty.", "empty_suggested_value");
        }

        if (string.Equals(currentValue, request.SuggestedValue, StringComparison.Ordinal))
        {
            return Failed("Suggested value is already applied.", "already_applied");
        }

        if (attribute is null)
        {
            elementResult.Element.SetAttributeValue(request.PropertyName, request.SuggestedValue);
        }
        else
        {
            attribute.Value = request.SuggestedValue;
        }

        return new UiPathXamlMutationResult
        {
            Success = true,
            Message = "DisplayName mutation was prepared.",
            PreviousValue = currentValue,
            NewValue = request.SuggestedValue,
            MutatedContent = Serialize(document)
        };
    }

    private static ElementLookupResult FindElement(XDocument document, UiPathActivityLocator locator, string? expectedCurrentValue)
    {
        if (document.Root is null)
        {
            return ElementLookupResult.Failed("XAML document does not have a root element.", "invalid_xaml");
        }

        var indexedElements = IndexActivityElements(document.Root).ToArray();
        if (!string.IsNullOrWhiteSpace(locator.IdRef))
        {
            var matches = indexedElements
                .Where(item => string.Equals(ReadAttributeValue(item.Element, "WorkflowViewState.IdRef") ?? ReadAttributeValue(item.Element, "IdRef"), locator.IdRef, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length == 1)
            {
                return ElementLookupResult.Found(matches[0].Element);
            }

            if (matches.Length > 1)
            {
                return ElementLookupResult.Failed("Activity locator matched multiple IdRef values.", "ambiguous_activity");
            }
        }

        if (!string.IsNullOrWhiteSpace(locator.ActivityPath))
        {
            var matches = indexedElements
                .Where(item => string.Equals(item.ActivityPath, locator.ActivityPath, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length == 1)
            {
                return ElementLookupResult.Found(matches[0].Element);
            }

            if (matches.Length > 1)
            {
                return ElementLookupResult.Failed("Activity locator matched multiple activity paths.", "ambiguous_activity");
            }
        }

        var fallbackMatches = indexedElements
            .Where(item =>
                (string.IsNullOrWhiteSpace(locator.XamlElementName) || NameComparer.Equals(NormalizeActivityName(item.Element.Name.LocalName), NormalizeActivityName(locator.XamlElementName)))
                && string.Equals(ReadAttributeValue(item.Element, "DisplayName"), expectedCurrentValue, StringComparison.Ordinal))
            .ToArray();

        return fallbackMatches.Length switch
        {
            1 => ElementLookupResult.Found(fallbackMatches[0].Element),
            0 => ElementLookupResult.Failed("Activity could not be found with the provided locator.", "activity_missing"),
            _ => ElementLookupResult.Failed("Activity locator is ambiguous; no mutation was applied.", "ambiguous_activity")
        };
    }

    private static IEnumerable<IndexedActivityElement> IndexActivityElements(XElement root)
    {
        if (IsWorkflowRootWrapper(root))
        {
            var rootChildIndex = 0;
            foreach (var child in root.Elements())
            {
                foreach (var item in IndexActivityElements(child, rootChildIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                {
                    yield return item;
                }

                rootChildIndex++;
            }

            yield break;
        }

        foreach (var item in IndexActivityElements(root, "0"))
        {
            yield return item;
        }
    }

    private static IEnumerable<IndexedActivityElement> IndexActivityElements(XElement element, string activityPath)
    {
        if (IsActivityElement(element))
        {
            yield return new IndexedActivityElement(element, activityPath);
        }

        var childIndex = 0;
        foreach (var child in element.Elements())
        {
            foreach (var item in IndexActivityElements(child, $"{activityPath}/{childIndex}"))
            {
                yield return item;
            }

            childIndex++;
        }
    }

    private static bool IsWorkflowRootWrapper(XElement element)
    {
        return element.Name.LocalName.Equals("Activity", StringComparison.OrdinalIgnoreCase)
            && ReadAttributeValue(element, "Class") is not null;
    }

    private static bool IsActivityElement(XElement element)
    {
        var localName = element.Name.LocalName;
        return !string.IsNullOrWhiteSpace(localName)
            && !localName.Contains('.', StringComparison.Ordinal)
            && !localName.Equals("Members", StringComparison.OrdinalIgnoreCase)
            && !localName.Equals("Property", StringComparison.OrdinalIgnoreCase)
            && !localName.Equals("References", StringComparison.OrdinalIgnoreCase);
    }

    private static XAttribute? FindAttribute(XElement element, string localName)
    {
        return element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ReadAttributeValue(XElement element, string localName)
    {
        return FindAttribute(element, localName)?.Value;
    }

    private static string NormalizeActivityName(string value)
    {
        return value.Replace("_", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static string Serialize(XDocument document)
    {
        using var textWriter = new Utf8StringWriter();
        using var xmlWriter = XmlWriter.Create(textWriter, new XmlWriterSettings
        {
            OmitXmlDeclaration = document.Declaration is null,
            Indent = false
        });
        document.Save(xmlWriter);
        xmlWriter.Flush();
        return textWriter.ToString();
    }

    private static UiPathXamlMutationResult Failed(string message, string? errorCode)
    {
        return new UiPathXamlMutationResult
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode
        };
    }

    private sealed record IndexedActivityElement(XElement Element, string ActivityPath);

    private sealed record ElementLookupResult(bool Success, XElement? Element, string Message, string ErrorCode)
    {
        public static ElementLookupResult Found(XElement element) => new(true, element, string.Empty, string.Empty);

        public static ElementLookupResult Failed(string message, string errorCode) => new(false, null, message, errorCode);
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override System.Text.Encoding Encoding => new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}

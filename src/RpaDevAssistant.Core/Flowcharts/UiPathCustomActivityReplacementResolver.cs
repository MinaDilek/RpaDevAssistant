using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Flowcharts;

public sealed record UiPathCustomActivityDetection
{
    public required string NodeId { get; init; }

    public required string ActivityName { get; init; }

    public string? DisplayName { get; init; }

    public string? CustomNamespace { get; init; }

    public string? CustomPackageFamily { get; init; }

    public required string SuggestedUiPathActivity { get; init; }

    public required string SuggestedPackage { get; init; }

    public string? ReplacementReason { get; init; }

    public bool CanAutoReplace { get; init; } = true;
}

public static class UiPathCustomActivityReplacementResolver
{
    private static readonly XNamespace UiPathNamespace = "http://schemas.uipath.com/workflow/activities";
    private static readonly XNamespace SystemNamespace = "http://schemas.microsoft.com/netfx/2009/xaml/activities";

    public static IReadOnlyList<UiPathCustomActivityDetection> DetectCustomActivities(
        UiPathFlowchartGraph graph,
        string? projectRoot = null)
    {
        var detections = new List<UiPathCustomActivityDetection>();
        var seenNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Nodes.Where(IsCustomActivity))
        {
            if (seenNodes.Add(node.Id))
            {
                detections.Add(CreateDetection(node));
            }
        }

        var resolvedRoot = projectRoot ?? (string.IsNullOrWhiteSpace(graph.WorkflowPath) ? null : Path.GetDirectoryName(Path.GetFullPath(graph.WorkflowPath)));
        if (!string.IsNullOrWhiteSpace(resolvedRoot))
        {
            var projectJsonPath = Path.Combine(resolvedRoot, "project.json");
            if (!File.Exists(projectJsonPath))
            {
                var dir = resolvedRoot;
                while (!string.IsNullOrWhiteSpace(dir))
                {
                    if (File.Exists(Path.Combine(dir, "project.json")))
                    {
                        projectJsonPath = Path.Combine(dir, "project.json");
                        break;
                    }

                    var parent = Directory.GetParent(dir);
                    if (parent is null || parent.FullName == dir) break;
                    dir = parent.FullName;
                }
            }

            if (File.Exists(projectJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(projectJsonPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("dependencies", out var depsElement) && depsElement.ValueKind == JsonValueKind.Object)
                    {
                        var dependencies = depsElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                        var customLibs = dependencies.Keys
                            .Where(k => !k.StartsWith("UiPath.", StringComparison.OrdinalIgnoreCase) && !k.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
                            .ToArray();

                        // Check for HttpClient / WebAPI activities
                        if (!dependencies.ContainsKey("UiPath.WebAPI.Activities"))
                        {
                            var httpNodes = graph.Nodes.Where(n =>
                                n.ActivityName?.Contains("HttpClient", StringComparison.OrdinalIgnoreCase) == true
                                || n.ActivityName?.Contains("HTTPRequest", StringComparison.OrdinalIgnoreCase) == true
                                || n.ActivityName?.Contains("DeserializeJson", StringComparison.OrdinalIgnoreCase) == true);

                            foreach (var node in httpNodes)
                            {
                                if (seenNodes.Add(node.Id))
                                {
                                    var customLibName = customLibs.FirstOrDefault(l => l.Contains("Custom", StringComparison.OrdinalIgnoreCase)) ?? customLibs.FirstOrDefault() ?? "CustomLibrary";
                                    detections.Add(new UiPathCustomActivityDetection
                                    {
                                        NodeId = node.Id,
                                        ActivityName = node.ActivityName ?? "ui:HttpClient",
                                        DisplayName = node.DisplayName ?? "HTTP Request",
                                        CustomNamespace = "http://schemas.uipath.com/workflow/activities",
                                        CustomPackageFamily = $"{customLibName} (Transitive Dependency)",
                                        SuggestedUiPathActivity = "ui:" + (node.ActivityName?.StartsWith("ui:", StringComparison.OrdinalIgnoreCase) == true ? node.ActivityName[3..] : (node.ActivityName ?? "HttpClient")),
                                        SuggestedPackage = "UiPath.WebAPI.Activities",
                                        ReplacementReason = customLibs.Length > 0
                                            ? $"Bu aktivite 'project.json' bağımlılıklarında doğrudan yer almamakta, '{string.Join(", ", customLibs)}' paketi üzerinden dolaylı (transitif) olarak gelmektedir. UiPath Studio'da HTTP Request aktivitesinin ve parametrelerinin sorunsuz çalışabilmesi için 'UiPath.WebAPI.Activities' paketi doğrudan proje bağımlılıklarına eklenmelidir."
                                            : "Bu aktivite için gerekli 'UiPath.WebAPI.Activities' paketi project.json bağımlılıklarında bulunmamaktadır. Doğrudan eklenmesi önerilir.",
                                        CanAutoReplace = true
                                    });
                                }
                            }
                        }

                        // Check for Database activities
                        if (!dependencies.ContainsKey("UiPath.Database.Activities"))
                        {
                            var dbNodes = graph.Nodes.Where(n =>
                                n.ActivityName?.Contains("ExecuteQuery", StringComparison.OrdinalIgnoreCase) == true
                                || n.ActivityName?.Contains("ExecuteNonQuery", StringComparison.OrdinalIgnoreCase) == true);

                            foreach (var node in dbNodes)
                            {
                                if (seenNodes.Add(node.Id))
                                {
                                    var customLibName = customLibs.FirstOrDefault() ?? "CustomLibrary";
                                    detections.Add(new UiPathCustomActivityDetection
                                    {
                                        NodeId = node.Id,
                                        ActivityName = node.ActivityName ?? "ui:ExecuteQuery",
                                        DisplayName = node.DisplayName ?? "Execute Query",
                                        CustomNamespace = "http://schemas.uipath.com/workflow/activities",
                                        CustomPackageFamily = $"{customLibName} (Transitive Dependency)",
                                        SuggestedUiPathActivity = "ui:" + (node.ActivityName?.StartsWith("ui:", StringComparison.OrdinalIgnoreCase) == true ? node.ActivityName[3..] : (node.ActivityName ?? "ExecuteQuery")),
                                        SuggestedPackage = "UiPath.Database.Activities",
                                        ReplacementReason = "Bu aktivite için 'UiPath.Database.Activities' paketinin doğrudan project.json dosyasına eklenmesi önerilir.",
                                        CanAutoReplace = true
                                    });
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore JSON parsing errors
                }
            }
        }

        return detections;
    }


    public static bool IsCustomActivity(UiPathFlowNode node)
    {
        if (UiPathFlowchartConversionPolicy.IsCommentedCodeBlock(node))
        {
            return false;
        }

        if (node.Type is UiPathFlowNodeType.Decision or UiPathFlowNodeType.Switch)
        {
            return false;
        }

        var activityName = node.ActivityName;
        if (string.IsNullOrWhiteSpace(activityName))
        {
            return false;
        }

        if (node.Properties.TryGetValue("__namespace", out var ns) && !string.IsNullOrWhiteSpace(ns))
        {
            if (IsCustomNamespace(ns))
            {
                return true;
            }
        }

        return IsCustomActivityName(activityName);
    }

    public static bool IsCustomNamespace(string? ns)
    {
        if (string.IsNullOrWhiteSpace(ns))
        {
            return false;
        }

        var trimmed = ns.Trim();
        if (trimmed.StartsWith("http://schemas.microsoft.com", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("http://schemas.uipath.com", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("clr-namespace:System", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("clr-namespace:UiPath", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return trimmed.StartsWith("clr-namespace:", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("assembly=", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCustomActivityName(string activityName)
    {
        var normalized = activityName.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

        var lastColon = normalized.LastIndexOf(':');
        if (lastColon >= 0 && lastColon < normalized.Length - 1)
        {
            normalized = normalized[(lastColon + 1)..];
        }

        var lastDot = normalized.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < normalized.Length - 1)
        {
            normalized = normalized[(lastDot + 1)..];
        }

        // Standard built-in activities
        string[] standardActivities =
        [
            "Sequence", "Flowchart", "FlowDecision", "FlowSwitch", "FlowStep",
            "Assign", "MultipleAssign", "Delay", "LogMessage", "WriteLine", "InvokeWorkflowFile",
            "TryCatch", "Throw", "Rethrow", "While", "DoWhile", "ForEach", "If", "Switch",
            "Click", "TypeInto", "GetText", "CheckAppState", "ElementExists", "UseApplicationBrowser",
            "OpenBrowser", "AttachBrowser", "UseExcelFile", "ExcelApplicationScope", "ReadRange",
            "WriteRange", "AppendRange", "ReadCell", "WriteCell", "SendSMTPMailMessage",
            "SendOutlookMailMessage", "GetOutlookMailMessages", "SendMail", "HTTPRequest", "HttpClient",
            "ExecuteQuery", "ExecuteNonQuery", "Connect", "Disconnect", "GetCredential", "AddCredential",
            "GetQueueItem", "AddQueueItem", "SetTransactionStatus", "GetTransactionItem",
            "DeserializeJson", "DeserializeJsonArray"
        ];

        if (standardActivities.Any(std => std.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Custom prefixes or keywords
        return normalized.StartsWith("Custom", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("BalaReva", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Acme", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Company", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Community", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("Helper", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("Utility", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("Custom", StringComparison.OrdinalIgnoreCase);
    }

    public static UiPathCustomActivityDetection CreateDetection(UiPathFlowNode node)
    {
        var activityName = node.ActivityName ?? "CustomActivity";
        var (suggestedActivity, suggestedPackage, reason) = ResolveSuggestedReplacement(activityName);

        node.Properties.TryGetValue("__namespace", out var customNamespace);

        return new UiPathCustomActivityDetection
        {
            NodeId = node.Id,
            ActivityName = activityName,
            DisplayName = node.DisplayName,
            CustomNamespace = customNamespace,
            CustomPackageFamily = ResolvePackageFamily(customNamespace, activityName),
            SuggestedUiPathActivity = suggestedActivity,
            SuggestedPackage = suggestedPackage,
            ReplacementReason = reason,
            CanAutoReplace = true
        };
    }

    public static (string SuggestedActivity, string SuggestedPackage, string Reason) ResolveSuggestedReplacement(string activityName)
    {
        var lower = activityName.ToLowerInvariant();

        if (lower.Contains("log") || lower.Contains("logger") || lower.Contains("trace") || lower.Contains("audit"))
        {
            return (
                "ui:LogMessage",
                "UiPath.System.Activities",
                "Replace custom logging with standard UiPath LogMessage for centralized Orchestrator logging."
            );
        }

        if (lower.Contains("http") || lower.Contains("rest") || lower.Contains("api") || lower.Contains("request") || lower.Contains("webclient"))
        {
            return (
                "ui:HttpClient",
                "UiPath.WebAPI.Activities",
                "Replace custom HTTP client with standard UiPath WebAPI HttpClient activity."
            );
        }

        if (lower.Contains("excel") || lower.Contains("spreadsheet") || lower.Contains("workbook") || lower.Contains("sheet"))
        {
            return (
                "ui:ReadRange",
                "UiPath.Excel.Activities",
                "Replace custom Excel helper with standard UiPath Excel Integration activity."
            );
        }

        if (lower.Contains("db") || lower.Contains("database") || lower.Contains("sql") || lower.Contains("query"))
        {
            return (
                "ui:ExecuteQuery",
                "UiPath.Database.Activities",
                "Replace custom database utility with standard UiPath Database ExecuteQuery activity."
            );
        }

        if (lower.Contains("json") || lower.Contains("deserialize") || lower.Contains("serialize"))
        {
            return (
                "ui:DeserializeJson",
                "UiPath.WebAPI.Activities",
                "Replace custom JSON serializer with standard UiPath WebAPI DeserializeJson activity."
            );
        }

        if (lower.Contains("mail") || lower.Contains("email") || lower.Contains("smtp") || lower.Contains("outlook"))
        {
            return (
                "ui:SendMail",
                "UiPath.Mail.Activities",
                "Replace custom email sender with standard UiPath Mail activity."
            );
        }

        if (lower.Contains("credential") || lower.Contains("vault") || lower.Contains("password") || lower.Contains("secret"))
        {
            return (
                "ui:GetCredential",
                "UiPath.Credentials.Activities",
                "Replace custom credential store with standard UiPath GetCredential activity."
            );
        }

        if (lower.Contains("delay") || lower.Contains("wait") || lower.Contains("sleep") || lower.Contains("pause"))
        {
            return (
                "Delay",
                "UiPath.System.Activities",
                "Replace custom sleep/wait activity with standard UiPath Delay activity."
            );
        }

        if (lower.Contains("assign") || lower.Contains("setvar") || lower.Contains("setvalue"))
        {
            return (
                "Assign",
                "UiPath.System.Activities",
                "Replace custom variable setter with standard UiPath Assign activity."
            );
        }

        return (
            "ui:InvokeWorkflowFile",
            "UiPath.System.Activities",
            "Encapsulate custom logic into a standalone workflow and invoke it with InvokeWorkflowFile."
        );
    }

    public static XElement CreateStandardUiPathActivityElement(string suggestedActivity, string? displayName, XElement? sourceElement = null)
    {
        var cleanDisplayName = displayName ?? suggestedActivity;
        var (ns, localName) = ParseQualifiedName(suggestedActivity);

        // If sourceElement is already of the target activity type, preserve the entire element and its children intact!
        if (sourceElement is not null && string.Equals(sourceElement.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
        {
            var preserved = new XElement(sourceElement);
            if (!string.IsNullOrWhiteSpace(cleanDisplayName))
            {
                preserved.SetAttributeValue("DisplayName", cleanDisplayName);
            }
            return preserved;
        }

        var element = new XElement(ns + localName);
        element.SetAttributeValue("DisplayName", cleanDisplayName);

        if (sourceElement is not null)
        {
            foreach (var attr in sourceElement.Attributes().Where(a => !a.IsNamespaceDeclaration && !a.Name.LocalName.Equals("DisplayName", StringComparison.OrdinalIgnoreCase)))
            {
                element.SetAttributeValue(attr.Name, attr.Value);
            }

            foreach (var child in sourceElement.Elements())
            {
                element.Add(new XElement(child));
            }
        }

        switch (localName.ToLowerInvariant())
        {
            case "logmessage":
                if (element.Attribute("Level") is null) element.SetAttributeValue("Level", "Info");
                if (element.Attribute("Message") is null) element.SetAttributeValue("Message", $"[\"Migrated custom activity: {cleanDisplayName}\"]");
                break;
            case "httpclient":
            case "httprequest":
                var endpoint = sourceElement?.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals("EndPoint", StringComparison.OrdinalIgnoreCase) || a.Name.LocalName.Equals("Endpoint", StringComparison.OrdinalIgnoreCase))?.Value;
                if (element.Attribute("EndPoint") is null && element.Attribute("Endpoint") is null)
                {
                    element.SetAttributeValue("EndPoint", endpoint ?? "https://api.example.com");
                }
                if (element.Attribute("TimeoutMS") is null) element.SetAttributeValue("TimeoutMS", "30000");
                break;
            case "readrange":
                if (element.Attribute("SheetName") is null) element.SetAttributeValue("SheetName", "Sheet1");
                break;
            case "executequery":
                if (element.Attribute("Sql") is null) element.SetAttributeValue("Sql", sourceElement?.Attribute("Query")?.Value ?? "SELECT 1");
                break;
            case "delay":
                if (element.Attribute("Duration") is null) element.SetAttributeValue("Duration", "00:00:01");
                break;
            case "invokeworkflowfile":
                if (element.Attribute("WorkflowFileName") is null) element.SetAttributeValue("WorkflowFileName", $"{cleanDisplayName.Replace(" ", string.Empty, StringComparison.Ordinal)}.xaml");
                break;
            case "deserializejson":
                if (element.Attribute("JsonString") is null) element.SetAttributeValue("JsonString", "{}");
                break;
            case "getcredential":
                if (element.Attribute("AssetName") is null) element.SetAttributeValue("AssetName", cleanDisplayName);
                break;
        }

        return element;
    }


    private static (XNamespace Namespace, string LocalName) ParseQualifiedName(string activityName)
    {
        if (activityName.StartsWith("ui:", StringComparison.OrdinalIgnoreCase))
        {
            return (UiPathNamespace, activityName[3..]);
        }

        if (activityName.Contains(':'))
        {
            var parts = activityName.Split(':');
            return (UiPathNamespace, parts[1]);
        }

        return (SystemNamespace, activityName);
    }

    private static string? ResolvePackageFamily(string? ns, string activityName)
    {
        if (!string.IsNullOrWhiteSpace(ns))
        {
            var assemblyIndex = ns.IndexOf("assembly=", StringComparison.OrdinalIgnoreCase);
            if (assemblyIndex >= 0)
            {
                var asmPart = ns[(assemblyIndex + "assembly=".Length)..].Trim();
                var commaIndex = asmPart.IndexOfAny([',', ';', ' ']);
                return commaIndex >= 0 ? asmPart[..commaIndex] : asmPart;
            }

            var clrIndex = ns.IndexOf("clr-namespace:", StringComparison.OrdinalIgnoreCase);
            if (clrIndex >= 0)
            {
                var clrPart = ns[(clrIndex + "clr-namespace:".Length)..].Trim();
                var semiIndex = clrPart.IndexOf(';');
                return semiIndex >= 0 ? clrPart[..semiIndex] : clrPart;
            }
        }

        return "Custom.Dependency";
    }

    public static void EnsureDirectDependencyInProjectJson(string projectRoot, string packageName, string packageVersion)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return;
        }

        var projectJsonPath = Path.Combine(projectRoot, "project.json");
        if (!File.Exists(projectJsonPath))
        {
            var dir = projectRoot;
            while (!string.IsNullOrWhiteSpace(dir))
            {
                var candidate = Path.Combine(dir, "project.json");
                if (File.Exists(candidate))
                {
                    projectJsonPath = candidate;
                    break;
                }

                var parent = Directory.GetParent(dir);
                if (parent is null || parent.FullName == dir) break;
                dir = parent.FullName;
            }
        }

        if (!File.Exists(projectJsonPath))
        {
            return;
        }

        try
        {
            var content = File.ReadAllText(projectJsonPath);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (!root.TryGetProperty("dependencies", out var dependencies))
            {
                return;
            }

            if (dependencies.TryGetProperty(packageName, out _))
            {
                return;
            }

            var node = JsonNode.Parse(content);
            if (node?["dependencies"] is JsonObject depsObj)
            {
                depsObj[packageName] = packageVersion;
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(projectJsonPath, node.ToJsonString(options));
            }
        }
        catch
        {
            // Ignore if cannot modify project.json
        }
    }
}

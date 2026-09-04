namespace RpaDevAssistant.Core.ProjectAssistant;

public static class UiPathActivityAliasNormalizer
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["delay"] = "Delay",
        ["logmessage"] = "LogMessage",
        ["log"] = "LogMessage",
        ["httprequest"] = "HTTPRequest",
        ["http"] = "HTTPRequest",
        ["getcredential"] = "GetCredential",
        ["credential"] = "GetCredential",
        ["invokeworkflow"] = "InvokeWorkflowFile",
        ["invokeworkflowfile"] = "InvokeWorkflowFile",
        ["queue"] = "Queue",
        ["addqueueitem"] = "AddQueueItem",
        ["gettransactionitem"] = "GetTransactionItem",
        ["asset"] = "Asset",
        ["getasset"] = "GetAsset",
        ["click"] = "Click",
        ["typeinto"] = "TypeInto",
        ["gettext"] = "GetText",
        ["assign"] = "Assign",
        ["throw"] = "Throw",
        ["rethrow"] = "Rethrow"
    };

    public static string NormalizeActivityName(string? value)
    {
        var compact = ProjectAssistantTextNormalizer.Compact(value);
        return Aliases.TryGetValue(compact, out var canonicalName) ? canonicalName : compact;
    }

    public static string? DetectActivityName(string question)
    {
        var normalizedQuestion = ProjectAssistantTextNormalizer.Normalize(question);
        var compactQuestion = ProjectAssistantTextNormalizer.Compact(question);

        foreach (var alias in Aliases.Keys.OrderByDescending(key => key.Length))
        {
            if (compactQuestion.Contains(alias, StringComparison.OrdinalIgnoreCase))
            {
                return Aliases[alias];
            }
        }

        foreach (var phrase in new[] { "http request", "get credential", "log message", "invoke workflow file", "invoke workflow" })
        {
            if (normalizedQuestion.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeActivityName(phrase);
            }
        }

        return null;
    }
}

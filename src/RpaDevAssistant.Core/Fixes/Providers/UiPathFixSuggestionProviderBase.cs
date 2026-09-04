namespace RpaDevAssistant.Core.Fixes.Providers;

public abstract class UiPathFixSuggestionProviderBase : IUiPathFixSuggestionProvider
{
    public abstract IReadOnlyCollection<string> SupportedRuleIds { get; }

    public virtual bool RequiresAi => false;

    public abstract UiPathFixSuggestion? Suggest(UiPathFixContext context);

    protected static string SuggestionId(UiPathFixContext context)
    {
        var value = $"{context.Finding.RuleId}|{context.Finding.WorkflowPath}|{context.Finding.ActivityId}|{context.Finding.PropertyName}|{context.Finding.CurrentValue}";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();
    }

    protected static UiPathPatchPreview TextPreview(string before, string after)
    {
        return new UiPathPatchPreview
        {
            Format = UiPathPatchPreviewFormat.InstructionOnly,
            Description = "Instruction-only preview. No XAML patch is generated.",
            Before = before,
            After = after,
            Notes = ["Review and apply manually in UiPath Studio."]
        };
    }

    protected static UiPathPatchPreview PropertyPreview(string name, string? before, string? after)
    {
        return new UiPathPatchPreview
        {
            Format = UiPathPatchPreviewFormat.PropertyChange,
            Description = "Property change preview.",
            Before = $"{name} = \"{before}\"",
            After = $"{name} = \"{after}\"",
            ChangedProperties =
            [
                new UiPathChangedProperty
                {
                    Name = name,
                    Before = before,
                    After = after
                }
            ]
        };
    }
}

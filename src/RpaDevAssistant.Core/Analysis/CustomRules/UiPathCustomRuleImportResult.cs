namespace RpaDevAssistant.Core.Analysis.CustomRules;

public sealed record UiPathCustomRuleImportResult
{
    public int ImportedCount { get; init; }

    public int SkippedDuplicateCount { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];
}

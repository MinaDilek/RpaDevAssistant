namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathBulkFixApplyResult
{
    public bool Success { get; init; }

    public int EligibleCount { get; init; }

    public int AppliedCount { get; init; }

    public int SkippedCount { get; init; }

    public bool RequiresReanalysis { get; init; }

    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<UiPathFixApplyResult> Results { get; init; } = [];
}

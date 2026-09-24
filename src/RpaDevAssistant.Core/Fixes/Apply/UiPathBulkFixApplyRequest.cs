namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathBulkFixApplyRequest
{
    public required string ProjectPath { get; init; }

    public string? ProfileId { get; init; }

    public string? Locale { get; init; }

    public int? MaxFixes { get; init; }

    public bool CreateBackup { get; init; } = true;
}

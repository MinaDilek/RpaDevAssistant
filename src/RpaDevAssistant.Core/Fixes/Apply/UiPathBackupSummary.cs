namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathBackupSummary
{
    public required string BackupId { get; init; }

    public DateTimeOffset? CreatedAtUtc { get; init; }

    public string? WorkflowPath { get; init; }

    public string? RuleId { get; init; }

    public string? PropertyName { get; init; }

    public string? PreviousValue { get; init; }

    public string? NewValue { get; init; }

    public string? OriginalHash { get; init; }

    public string? ModifiedHash { get; init; }

    public UiPathBackupStatus Status { get; init; }

    public bool CanUndo { get; init; }

    public string? Reason { get; init; }
}

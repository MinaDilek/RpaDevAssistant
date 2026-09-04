namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathUndoResult
{
    public bool Success { get; init; }

    public bool Restored { get; init; }

    public string? OperationId { get; init; }

    public required string Message { get; init; }

    public string? BackupId { get; init; }

    public string? WorkflowPath { get; init; }

    public string? PreviousHash { get; init; }

    public string? RestoredHash { get; init; }

    public string? SafetyBackupId { get; init; }

    public DateTimeOffset? RestoredAtUtc { get; init; }

    public bool RequiresReanalysis { get; init; }

    public string? ErrorCode { get; init; }

    public UiPathFixApplyValidationResult ValidationResult { get; init; } = new();
}

namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathFixApplyResult
{
    public bool Success { get; init; }

    public bool Applied { get; init; }

    public string? OperationId { get; init; }

    public required string Message { get; init; }

    public string? WorkflowPath { get; init; }

    public string? RuleId { get; init; }

    public string? PropertyName { get; init; }

    public string? PreviousValue { get; init; }

    public string? NewValue { get; init; }

    public string? BackupPath { get; init; }

    public string? BackupId { get; init; }

    public UiPathFixApplyValidationResult ValidationResult { get; init; } = new();

    public DateTimeOffset? AppliedAtUtc { get; init; }

    public string? ErrorCode { get; init; }

    public bool RequiresReanalysis { get; init; }
}

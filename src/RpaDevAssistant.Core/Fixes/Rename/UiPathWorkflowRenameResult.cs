namespace RpaDevAssistant.Core.Fixes.Rename;

public sealed record UiPathWorkflowRenameResult
{
    public bool Success { get; init; }

    public bool Renamed { get; init; }

    public required string Message { get; init; }

    public string? OperationId { get; init; }

    public string? PreviousWorkflowPath { get; init; }

    public string? NewWorkflowPath { get; init; }

    public IReadOnlyList<string> UpdatedCallerWorkflows { get; init; } = [];

    public IReadOnlyList<string> DynamicReferencesRequiringReview { get; init; } = [];

    public string? BackupId { get; init; }

    public string? BackupPath { get; init; }

    public string? ErrorCode { get; init; }

    public bool RolledBack { get; init; }

    public bool RequiresReanalysis { get; init; }
}

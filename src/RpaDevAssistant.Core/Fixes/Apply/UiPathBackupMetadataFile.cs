namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathBackupMetadataFile
{
    public required string WorkflowPath { get; init; }

    public required string OriginalHash { get; init; }

    public required string ModifiedHash { get; init; }

    public required string AppliedRuleId { get; init; }

    public required string PropertyName { get; init; }

    public string? PreviousValue { get; init; }

    public string? NewValue { get; init; }
}

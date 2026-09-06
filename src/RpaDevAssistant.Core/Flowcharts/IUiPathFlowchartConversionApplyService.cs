using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Flowcharts;

public interface IUiPathFlowchartConversionApplyService
{
    Task<UiPathFlowchartConversionApplyResult> ApplyAsync(UiPathFlowchartConversionApplyRequest request, CancellationToken cancellationToken = default);

    Task<UiPathFlowchartConversionRollbackResult> RollbackAsync(UiPathFlowchartConversionRollbackRequest request, CancellationToken cancellationToken = default);

    UiPathFlowchartGeneratedContent GenerateConvertedContent(
        string workflowFullPath,
        string projectPath,
        string workflowPath,
        UiPathFlowchartGraph graph,
        UiPathWorkflowStructureType expectedRootStructure = UiPathWorkflowStructureType.Sequence);
}

public sealed record UiPathFlowchartGeneratedContent(string? Content, RpaDevAssistant.Core.Fixes.Apply.UiPathFixApplyValidationResult Validation, IReadOnlyList<string> Warnings);

public sealed record UiPathFlowchartConversionApplyRequest
{
    public required string ProjectPath { get; init; }

    public required string WorkflowPath { get; init; }

    public string? ExpectedWorkflowHash { get; init; }

    public bool Confirmed { get; init; }

    public bool CreateBackup { get; init; } = true;
}

public sealed record UiPathFlowchartConversionRollbackRequest
{
    public required string ProjectPath { get; init; }

    public required string WorkflowPath { get; init; }

    public required string BackupId { get; init; }

    public string? ExpectedCurrentHash { get; init; }

    public bool CreateSafetyBackup { get; init; } = true;
}

public sealed record UiPathFlowchartConversionApplyResult
{
    public bool Success { get; init; }

    public bool Applied { get; init; }

    public required string Message { get; init; }

    public string? WorkflowPath { get; init; }

    public string? OriginalHash { get; init; }

    public string? ConvertedHash { get; init; }

    public string? BackupId { get; init; }

    public string? BackupPath { get; init; }

    public UiPathFixApplyValidationResult ValidationResult { get; init; } = new();

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public bool RollbackAvailable { get; init; }

    public bool RequiresReanalysis { get; init; }

    public string? ErrorCode { get; init; }

    public DateTimeOffset? AppliedAtUtc { get; init; }
}

public sealed record UiPathFlowchartConversionRollbackResult
{
    public bool Success { get; init; }

    public bool Restored { get; init; }

    public required string Message { get; init; }

    public string? BackupId { get; init; }

    public string? WorkflowPath { get; init; }

    public string? PreviousHash { get; init; }

    public string? RestoredHash { get; init; }

    public string? SafetyBackupId { get; init; }

    public UiPathFixApplyValidationResult ValidationResult { get; init; } = new();

    public bool RequiresReanalysis { get; init; }

    public string? ErrorCode { get; init; }
}

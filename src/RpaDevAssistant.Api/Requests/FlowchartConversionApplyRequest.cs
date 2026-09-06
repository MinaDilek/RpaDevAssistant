namespace RpaDevAssistant.Api.Requests;

public sealed record FlowchartConversionApplyRequest
{
    public string? ProjectPath { get; init; }

    public string? WorkflowPath { get; init; }

    public string? ExpectedWorkflowHash { get; init; }

    public bool Confirmed { get; init; }

    public bool CreateBackup { get; init; } = true;
}

public sealed record FlowchartConversionRollbackRequest
{
    public string? ProjectPath { get; init; }

    public string? WorkflowPath { get; init; }

    public string? BackupId { get; init; }

    public string? ExpectedCurrentHash { get; init; }

    public bool CreateSafetyBackup { get; init; } = true;
}

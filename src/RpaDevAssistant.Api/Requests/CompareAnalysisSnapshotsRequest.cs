namespace RpaDevAssistant.Api.Requests;

public sealed record CompareAnalysisSnapshotsRequest
{
    public string? ProjectPath { get; init; }

    public string? BaselineSnapshotId { get; init; }

    public string? TargetSnapshotId { get; init; }
}

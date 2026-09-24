namespace RpaDevAssistant.Core.Orchestrator;

public sealed record UiPathOrchestratorSummary
{
    public bool Configured { get; init; }
    public bool Success { get; init; }
    public required string Message { get; init; }
    public string? DeploymentType { get; init; }
    public IReadOnlyList<UiPathOrchestratorProcess> Processes { get; init; } = [];
    public IReadOnlyList<UiPathOrchestratorQueue> Queues { get; init; } = [];
    public IReadOnlyList<UiPathOrchestratorAsset> Assets { get; init; } = [];
    public IReadOnlyList<UiPathOrchestratorMachine> Machines { get; init; } = [];
    public string? ErrorCode { get; init; }
}

public sealed record UiPathOrchestratorProcess(long? Id, string Name, string? ProcessKey, string? Version, bool? IsLatestVersion);
public sealed record UiPathOrchestratorQueue(long? Id, string Name, string? Description, int? MaxRetries);
public sealed record UiPathOrchestratorAsset(long? Id, string Name, string? ValueScope, string? ValueType);
public sealed record UiPathOrchestratorMachine(long? Id, string Name, string? Type);

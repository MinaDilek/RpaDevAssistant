namespace RpaDevAssistant.Infrastructure.Orchestrator;

public sealed record UiPathOrchestratorOptions
{
    public Uri? BaseUri { get; init; }
    public string? AccessToken { get; init; }
    public string? TenantName { get; init; }
    public long? FolderId { get; init; }
    public string DeploymentType { get; init; } = "AutomationCloud";
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public bool IsConfigured => BaseUri is not null && !string.IsNullOrWhiteSpace(AccessToken);
}

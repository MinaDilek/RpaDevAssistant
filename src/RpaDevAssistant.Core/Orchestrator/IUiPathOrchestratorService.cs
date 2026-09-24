namespace RpaDevAssistant.Core.Orchestrator;

public interface IUiPathOrchestratorService
{
    Task<UiPathOrchestratorSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}

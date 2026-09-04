namespace RpaDevAssistant.Core.Ai;

public interface IUiPathAiReviewService
{
    Task<UiPathAiReviewResult> ReviewAsync(
        string projectPath,
        string? profileId,
        UiPathAiReviewScope scope,
        string? workflowPath,
        string? additionalInstructions,
        CancellationToken cancellationToken,
        string? locale = null);
}

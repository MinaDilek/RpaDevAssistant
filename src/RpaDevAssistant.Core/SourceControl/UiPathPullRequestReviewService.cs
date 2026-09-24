using RpaDevAssistant.Core.Git;

namespace RpaDevAssistant.Core.SourceControl;

public sealed class UiPathPullRequestReviewService(IUiPathSourceControlClient client, IUiPathGitComparisonService comparison) : IUiPathPullRequestReviewService
{
    public async Task<UiPathPullRequestReviewResult> ReviewAsync(UiPathPullRequestReviewRequest request, CancellationToken cancellationToken = default)
    {
        if (!client.IsConfigured(request.Provider)) return Failure(request, "SOURCE_CONTROL_NOT_CONFIGURED", $"{request.Provider} integration is not configured.");
        if (string.IsNullOrWhiteSpace(request.Repository) || request.PullRequestId <= 0) return Failure(request, "INVALID_PULL_REQUEST", "Repository and a positive pull request ID are required.");
        try
        {
            var metadata = await client.GetPullRequestAsync(request.Provider, request.Repository, request.PullRequestId, cancellationToken).ConfigureAwait(false);
            var result = await comparison.CompareAsync(new UiPathGitComparisonRequest
            {
                ProjectPath = request.ProjectPath,
                BaselineRef = metadata.BaseCommit,
                TargetRef = metadata.HeadCommit,
                ProfileId = request.ProfileId
            }, cancellationToken).ConfigureAwait(false);
            return new UiPathPullRequestReviewResult
            {
                Success = result.Success,
                Message = result.Success ? "Pull request analysis completed." : result.Message,
                Provider = request.Provider, Repository = request.Repository, PullRequestId = request.PullRequestId,
                PullRequest = metadata, Comparison = result,
                ErrorCode = result.Success ? null : result.ErrorCode
            };
        }
        catch (HttpRequestException error)
        {
            return Failure(request, "SOURCE_CONTROL_UNAVAILABLE", $"{request.Provider} request failed (HTTP {(int?)error.StatusCode ?? 0}).");
        }
        catch (System.Text.Json.JsonException)
        {
            return Failure(request, "SOURCE_CONTROL_INVALID_RESPONSE", $"{request.Provider} returned an invalid response.");
        }
    }

    public async Task<UiPathPullRequestCommentResult> CommentAsync(UiPathPullRequestCommentRequest request, CancellationToken cancellationToken = default)
    {
        if (!client.IsConfigured(request.Provider)) return new(false, $"{request.Provider} integration is not configured.", "SOURCE_CONTROL_NOT_CONFIGURED");
        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 60_000) return new(false, "Comment must contain 1 to 60000 characters.", "INVALID_COMMENT");
        try
        {
            await client.PostCommentAsync(request, cancellationToken).ConfigureAwait(false);
            return new(true, "Review comment published.");
        }
        catch (HttpRequestException error)
        {
            return new(false, $"{request.Provider} comment request failed (HTTP {(int?)error.StatusCode ?? 0}).", "SOURCE_CONTROL_UNAVAILABLE");
        }
    }

    private static UiPathPullRequestReviewResult Failure(UiPathPullRequestReviewRequest request, string code, string message) => new()
    {
        Success = false, Message = message, Provider = request.Provider, Repository = request.Repository,
        PullRequestId = request.PullRequestId, ErrorCode = code
    };
}

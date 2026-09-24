namespace RpaDevAssistant.Core.SourceControl;

public interface IUiPathSourceControlClient
{
    Task<UiPathPullRequestMetadata> GetPullRequestAsync(UiPathSourceControlProvider provider, string repository, int pullRequestId, CancellationToken cancellationToken);
    Task PostCommentAsync(UiPathPullRequestCommentRequest request, CancellationToken cancellationToken);
    bool IsConfigured(UiPathSourceControlProvider provider);
}

public interface IUiPathPullRequestReviewService
{
    Task<UiPathPullRequestReviewResult> ReviewAsync(UiPathPullRequestReviewRequest request, CancellationToken cancellationToken = default);
    Task<UiPathPullRequestCommentResult> CommentAsync(UiPathPullRequestCommentRequest request, CancellationToken cancellationToken = default);
}

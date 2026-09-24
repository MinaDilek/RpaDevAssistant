using RpaDevAssistant.Core.Git;

namespace RpaDevAssistant.Core.SourceControl;

public enum UiPathSourceControlProvider { GitHub, GitLab, AzureDevOps }

public sealed record UiPathPullRequestReviewRequest
{
    public required string ProjectPath { get; init; }
    public required UiPathSourceControlProvider Provider { get; init; }
    public required string Repository { get; init; }
    public required int PullRequestId { get; init; }
    public string? ProfileId { get; init; }
}

public sealed record UiPathPullRequestMetadata(string Title, string BaseCommit, string HeadCommit, string? WebUrl);

public sealed record UiPathPullRequestReviewResult
{
    public bool Success { get; init; }
    public required string Message { get; init; }
    public UiPathSourceControlProvider Provider { get; init; }
    public string? Repository { get; init; }
    public int PullRequestId { get; init; }
    public UiPathPullRequestMetadata? PullRequest { get; init; }
    public UiPathGitComparisonResult? Comparison { get; init; }
    public string? ErrorCode { get; init; }
}

public sealed record UiPathPullRequestCommentRequest(UiPathSourceControlProvider Provider, string Repository, int PullRequestId, string Body);
public sealed record UiPathPullRequestCommentResult(bool Success, string Message, string? ErrorCode = null);

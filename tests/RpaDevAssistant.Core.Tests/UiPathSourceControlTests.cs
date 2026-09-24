using System.Net;
using System.Text;
using RpaDevAssistant.Core.Git;
using RpaDevAssistant.Core.SourceControl;
using RpaDevAssistant.Infrastructure.SourceControl;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathSourceControlTests
{
    [Theory]
    [InlineData(UiPathSourceControlProvider.GitHub, "owner/repo", "/repos/owner/repo/pulls/7", "{\"title\":\"PR\",\"base\":{\"sha\":\"base\"},\"head\":{\"sha\":\"head\"},\"html_url\":\"https://example/pr/7\"}")]
    [InlineData(UiPathSourceControlProvider.GitLab, "group/repo", "/api/v4/projects/group%2Frepo/merge_requests/7", "{\"title\":\"MR\",\"diff_refs\":{\"base_sha\":\"base\",\"head_sha\":\"head\"},\"web_url\":\"https://example/mr/7\"}")]
    [InlineData(UiPathSourceControlProvider.AzureDevOps, "repo", "/project/_apis/git/repositories/repo/pullrequests/7", "{\"title\":\"PR\",\"lastMergeTargetCommit\":{\"commitId\":\"base\"},\"lastMergeSourceCommit\":{\"commitId\":\"head\"},\"url\":\"https://example/pr/7\"}")]
    public async Task ReadsPullRequestMetadataForEachProvider(UiPathSourceControlProvider provider, string repository, string expectedPath, string json)
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        var client = new UiPathSourceControlClient(new HttpClient(handler), Options(provider));

        var result = await client.GetPullRequestAsync(provider, repository, 7, CancellationToken.None);

        Assert.Equal("base", result.BaseCommit);
        Assert.Equal("head", result.HeadCommit);
        Assert.Equal(expectedPath, handler.Requests.Single().Uri.AbsolutePath);
        Assert.DoesNotContain("token-value", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData(UiPathSourceControlProvider.GitHub, "/repos/owner/repo/issues/7/comments")]
    [InlineData(UiPathSourceControlProvider.GitLab, "/api/v4/projects/owner%2Frepo/merge_requests/7/notes")]
    [InlineData(UiPathSourceControlProvider.AzureDevOps, "/project/_apis/git/repositories/owner%2Frepo/pullRequests/7/threads")]
    public async Task PostsExplicitReviewComment(UiPathSourceControlProvider provider, string expectedPath)
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{}") });
        var client = new UiPathSourceControlClient(new HttpClient(handler), Options(provider));

        await client.PostCommentAsync(new UiPathPullRequestCommentRequest(provider, "owner/repo", 7, "Quality score: 85"), CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(expectedPath, request.Uri.AbsolutePath);
        Assert.Contains("Quality score: 85", request.Body);
    }

    [Fact]
    public async Task ReviewUsesProviderCommitsWithReadOnlyGitComparison()
    {
        var comparison = new FakeComparison();
        var service = new UiPathPullRequestReviewService(new FakeClient(), comparison);

        var result = await service.ReviewAsync(new UiPathPullRequestReviewRequest
        {
            ProjectPath = "/repo/project", Provider = UiPathSourceControlProvider.GitHub,
            Repository = "owner/repo", PullRequestId = 7, ProfileId = "strict"
        });

        Assert.True(result.Success);
        Assert.Equal("base-sha", comparison.Request?.BaselineRef);
        Assert.Equal("head-sha", comparison.Request?.TargetRef);
        Assert.Equal("/repo/project", comparison.Request?.ProjectPath);
    }

    private static UiPathSourceControlOptions Options(UiPathSourceControlProvider provider)
    {
        var baseUri = provider switch
        {
            UiPathSourceControlProvider.GitHub => new Uri("https://api.github.test/"),
            UiPathSourceControlProvider.GitLab => new Uri("https://gitlab.test/api/v4/"),
            _ => new Uri("https://dev.azure.test/project/")
        };
        return new UiPathSourceControlOptions { Providers = new Dictionary<UiPathSourceControlProvider, UiPathSourceControlProviderOptions> { [provider] = new(baseUri, "token-value") } };
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<Request> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new Request(request.RequestUri!, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return responder(request);
        }
    }
    private sealed record Request(Uri Uri, string Body);

    private sealed class FakeClient : IUiPathSourceControlClient
    {
        public bool IsConfigured(UiPathSourceControlProvider provider) => true;
        public Task<UiPathPullRequestMetadata> GetPullRequestAsync(UiPathSourceControlProvider provider, string repository, int pullRequestId, CancellationToken cancellationToken) => Task.FromResult(new UiPathPullRequestMetadata("PR", "base-sha", "head-sha", null));
        public Task PostCommentAsync(UiPathPullRequestCommentRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class FakeComparison : IUiPathGitComparisonService
    {
        public UiPathGitComparisonRequest? Request { get; private set; }
        public Task<UiPathGitComparisonResult> CompareAsync(UiPathGitComparisonRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new UiPathGitComparisonResult { Success = true, Message = "ok" });
        }
    }
}

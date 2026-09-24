using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RpaDevAssistant.Core.SourceControl;

namespace RpaDevAssistant.Infrastructure.SourceControl;

public sealed class UiPathSourceControlClient(HttpClient httpClient, UiPathSourceControlOptions options) : IUiPathSourceControlClient
{
    public bool IsConfigured(UiPathSourceControlProvider provider) => options.Providers.TryGetValue(provider, out var value) && value.IsConfigured;

    public async Task<UiPathPullRequestMetadata> GetPullRequestAsync(UiPathSourceControlProvider provider, string repository, int pullRequestId, CancellationToken cancellationToken)
    {
        var config = Config(provider);
        using var request = CreateRequest(provider, config, HttpMethod.Get, PullRequestPath(provider, repository, pullRequestId));
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        return provider switch
        {
            UiPathSourceControlProvider.GitHub => new(Text(root, "title"), NestedText(root, "base", "sha"), NestedText(root, "head", "sha"), OptionalText(root, "html_url")),
            UiPathSourceControlProvider.GitLab => new(Text(root, "title"), NestedText(root, "diff_refs", "base_sha"), NestedText(root, "diff_refs", "head_sha"), OptionalText(root, "web_url")),
            UiPathSourceControlProvider.AzureDevOps => new(Text(root, "title"), NestedText(root, "lastMergeTargetCommit", "commitId"), NestedText(root, "lastMergeSourceCommit", "commitId"), OptionalText(root, "url")),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }

    public async Task PostCommentAsync(UiPathPullRequestCommentRequest value, CancellationToken cancellationToken)
    {
        var config = Config(value.Provider);
        var path = CommentPath(value.Provider, value.Repository, value.PullRequestId);
        var payload = value.Provider == UiPathSourceControlProvider.AzureDevOps
            ? JsonSerializer.Serialize(new { comments = new[] { new { parentCommentId = 0, content = value.Body, commentType = 1 } }, status = 1 })
            : JsonSerializer.Serialize(new { body = value.Body });
        using var request = CreateRequest(value.Provider, config, HttpMethod.Post, path);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.RequestTimeout);
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) { response.Dispose(); throw new HttpRequestException("Source control request failed.", null, response.StatusCode); }
        return response;
    }

    private static HttpRequestMessage CreateRequest(UiPathSourceControlProvider provider, UiPathSourceControlProviderOptions config, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(config.BaseUri, path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("RPA-Dev-Assistant/0.1");
        if (provider == UiPathSourceControlProvider.GitLab) request.Headers.TryAddWithoutValidation("PRIVATE-TOKEN", config.AccessToken);
        else if (provider == UiPathSourceControlProvider.AzureDevOps) request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{config.AccessToken}")));
        else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);
        return request;
    }

    private UiPathSourceControlProviderOptions Config(UiPathSourceControlProvider provider) => options.Providers.TryGetValue(provider, out var value) && value.IsConfigured ? value : throw new InvalidOperationException($"{provider} is not configured.");
    private static string PullRequestPath(UiPathSourceControlProvider p, string r, int id) => p switch
    {
        UiPathSourceControlProvider.GitHub => $"repos/{SafeRepository(r)}/pulls/{id}",
        UiPathSourceControlProvider.GitLab => $"projects/{Uri.EscapeDataString(r)}/merge_requests/{id}",
        UiPathSourceControlProvider.AzureDevOps => $"_apis/git/repositories/{Uri.EscapeDataString(r)}/pullrequests/{id}?api-version=7.1",
        _ => throw new ArgumentOutOfRangeException(nameof(p))
    };
    private static string CommentPath(UiPathSourceControlProvider p, string r, int id) => p switch
    {
        UiPathSourceControlProvider.GitHub => $"repos/{SafeRepository(r)}/issues/{id}/comments",
        UiPathSourceControlProvider.GitLab => $"projects/{Uri.EscapeDataString(r)}/merge_requests/{id}/notes",
        UiPathSourceControlProvider.AzureDevOps => $"_apis/git/repositories/{Uri.EscapeDataString(r)}/pullRequests/{id}/threads?api-version=7.1",
        _ => throw new ArgumentOutOfRangeException(nameof(p))
    };
    private static string SafeRepository(string value)
    {
        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts.Any(part => part is "." or "..")) throw new ArgumentException("GitHub repository must use owner/name format.");
        return string.Join('/', parts.Select(Uri.EscapeDataString));
    }
    private static string Text(JsonElement root, string name) => OptionalText(root, name) ?? throw new JsonException($"Missing {name}.");
    private static string NestedText(JsonElement root, string parent, string name) => root.TryGetProperty(parent, out var node) ? Text(node, name) : throw new JsonException($"Missing {parent}.");
    private static string? OptionalText(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

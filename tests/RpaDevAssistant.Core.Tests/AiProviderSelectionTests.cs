using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.ProjectAssistant;
using RpaDevAssistant.Infrastructure.OpenAI;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class AiProviderSelectionTests
{
    [Fact]
    public void OpenAi_RemainsTheDefaultProvider()
    {
        var options = new OpenAiReviewOptions { ApiKey = "test-key" };

        var configured = options.TryResolveProvider(out var configuration);

        Assert.True(configured);
        Assert.Equal("OpenAI", configuration.ProviderName);
        Assert.Equal("https://api.openai.com/v1/responses", configuration.Endpoint.AbsoluteUri);
        Assert.Equal("test-key", configuration.ApiKey);
    }

    [Theory]
    [InlineData("http://localhost:11434/v1/responses")]
    [InlineData("http://127.0.0.1:11434/v1/responses")]
    [InlineData("http://127.42.1.9:11434/v1/responses")]
    [InlineData("http://[::1]:11434/v1/responses")]
    public void LocalProvider_AcceptsOnlyExplicitLoopbackEndpoints(string endpoint)
    {
        var options = LocalOptions(endpoint);

        Assert.True(options.TryResolveProvider(out var configuration));
        Assert.Equal("Local OpenAI-compatible", configuration.ProviderName);
        Assert.Equal(new Uri(endpoint), configuration.Endpoint);
        Assert.Null(configuration.ApiKey);
        Assert.Equal("local-test-model", options.EffectiveModel);
    }

    [Theory]
    [InlineData("https://api.example.com/v1/responses")]
    [InlineData("http://192.168.1.20:11434/v1/responses")]
    [InlineData("http://localhost.evil.example/v1/responses")]
    [InlineData("http://user:secret@localhost:11434/v1/responses")]
    [InlineData("http://localhost:11434/v1/responses?target=remote")]
    public void LocalProvider_RejectsRemoteOrUnsafeEndpoints(string endpoint)
    {
        var options = LocalOptions(endpoint);

        Assert.False(options.TryResolveProvider(out _));
    }

    [Fact]
    public async Task LocalProviderSelection_IsSharedByReviewAskProjectAndAiFix()
    {
        var handler = new RecordingHandler();
        var client = new HttpClient(handler);
        var options = Options.Create(LocalOptions("http://127.0.0.1:11434/v1/responses"));
        var review = new OpenAiUiPathReviewProvider(client, options, NullLogger<OpenAiUiPathReviewProvider>.Instance);
        var ask = new OpenAiUiPathProjectAssistantProvider(client, options, NullLogger<OpenAiUiPathProjectAssistantProvider>.Instance);
        var fix = new OpenAiUiPathFixAdvisor(client, options, NullLogger<OpenAiUiPathFixAdvisor>.Instance);

        await review.ReviewAsync(new UiPathAiPrompt
        {
            Scope = UiPathAiReviewScope.Project,
            SystemInstructions = "system",
            UserContext = "context"
        }, CancellationToken.None);
        await ask.AnswerAsync(new UiPathProjectAssistantPrompt
        {
            SystemInstructions = "system",
            UserContext = "context",
            Question = "question"
        }, CancellationToken.None);
        await fix.SuggestAsync(new UiPathAiFixPrompt
        {
            SystemInstructions = "system",
            UserContext = "context"
        }, CancellationToken.None);

        Assert.True(review.IsConfigured);
        Assert.True(ask.IsConfigured);
        Assert.True(fix.IsConfigured);
        Assert.Equal("Local OpenAI-compatible", review.ProviderName);
        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("http://127.0.0.1:11434/v1/responses", request.Uri.AbsoluteUri);
            Assert.Null(request.Authorization);
        });
    }

    [Fact]
    public async Task RejectedLocalEndpoint_DoesNotSendAnyProviderRequest()
    {
        var handler = new RecordingHandler();
        var provider = new OpenAiUiPathReviewProvider(
            new HttpClient(handler),
            Options.Create(LocalOptions("https://remote.example/v1/responses")),
            NullLogger<OpenAiUiPathReviewProvider>.Instance);

        var result = await provider.ReviewAsync(new UiPathAiPrompt
        {
            Scope = UiPathAiReviewScope.Project,
            SystemInstructions = "system",
            UserContext = "context"
        }, CancellationToken.None);

        Assert.False(provider.IsConfigured);
        Assert.False(result.IsConfigured);
        Assert.Empty(handler.Requests);
    }

    private static OpenAiReviewOptions LocalOptions(string endpoint) => new()
    {
        Provider = AiProviderKind.Local,
        LocalEndpoint = endpoint,
        LocalModel = "local-test-model"
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(Uri Uri, string? Authorization)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!, request.Headers.Authorization?.ToString()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}

using System.Net;
using System.Text;
using RpaDevAssistant.Infrastructure.Orchestrator;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathOrchestratorServiceTests
{
    [Fact]
    public async Task ReturnsExplicitNotConfiguredResultWithoutNetworkCall()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Network must not be called."));
        var service = new UiPathOrchestratorService(new HttpClient(handler), new UiPathOrchestratorOptions());

        var result = await service.GetSummaryAsync();

        Assert.False(result.Configured);
        Assert.Equal("ORCHESTRATOR_NOT_CONFIGURED", result.ErrorCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task LoadsReadOnlyInventoryAndSendsFolderHeadersWithoutExposingToken()
    {
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = path switch
            {
                "/odata/Releases" => "{\"value\":[{\"Id\":1,\"Name\":\"Invoice\",\"ProcessKey\":\"InvoiceKey\",\"ProcessVersion\":\"1.2.0\",\"IsLatestVersion\":true}]}",
                "/odata/QueueDefinitions" => "{\"value\":[{\"Id\":2,\"Name\":\"Invoices\",\"MaxNumberOfRetries\":3}]}",
                "/odata/Assets" => "{\"value\":[{\"Id\":3,\"Name\":\"ApiEndpoint\",\"ValueScope\":\"Global\",\"ValueType\":\"Text\"}]}",
                "/odata/Machines" => "{\"value\":[{\"Id\":4,\"Name\":\"RobotPool\",\"Type\":\"Template\"}]}",
                _ => throw new InvalidOperationException(path)
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        });
        var service = new UiPathOrchestratorService(new HttpClient(handler), new UiPathOrchestratorOptions
        {
            BaseUri = new Uri("https://cloud.example.test/"),
            AccessToken = "secret-token",
            TenantName = "Finance",
            FolderId = 42
        });

        var result = await service.GetSummaryAsync();

        Assert.True(result.Success);
        Assert.Single(result.Processes);
        Assert.Single(result.Queues);
        Assert.Single(result.Assets);
        Assert.Single(result.Machines);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("Bearer", request.AuthorizationScheme);
            Assert.Equal("secret-token", request.AuthorizationParameter);
            Assert.Equal("Finance", request.Tenant);
            Assert.Equal("42", request.Folder);
        });
        Assert.DoesNotContain("secret-token", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task ReturnsSafeFailureWithoutRawProviderBody()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("provider-secret-error")
        });
        var service = new UiPathOrchestratorService(new HttpClient(handler), new UiPathOrchestratorOptions
        {
            BaseUri = new Uri("https://cloud.example.test/"),
            AccessToken = "bad-token"
        });

        var result = await service.GetSummaryAsync();

        Assert.False(result.Success);
        Assert.Equal("ORCHESTRATOR_UNAVAILABLE", result.ErrorCode);
        Assert.DoesNotContain("provider-secret-error", result.Message);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<RequestRecord> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RequestRecord(
                request.RequestUri!,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues("X-UIPATH-TenantName", out var tenant) ? tenant.Single() : null,
                request.Headers.TryGetValues("X-UIPATH-OrganizationUnitId", out var folder) ? folder.Single() : null));
            return Task.FromResult(responder(request));
        }
    }

    private sealed record RequestRecord(Uri Uri, string? AuthorizationScheme, string? AuthorizationParameter, string? Tenant, string? Folder);
}

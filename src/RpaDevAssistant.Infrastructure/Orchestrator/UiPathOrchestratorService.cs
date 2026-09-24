using System.Net.Http.Headers;
using System.Text.Json;
using RpaDevAssistant.Core.Orchestrator;

namespace RpaDevAssistant.Infrastructure.Orchestrator;

public sealed class UiPathOrchestratorService : IUiPathOrchestratorService
{
    private readonly HttpClient httpClient;
    private readonly UiPathOrchestratorOptions options;

    public UiPathOrchestratorService(HttpClient httpClient, UiPathOrchestratorOptions options)
    {
        this.httpClient = httpClient;
        this.options = options;
    }

    public async Task<UiPathOrchestratorSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        if (!options.IsConfigured)
        {
            return new UiPathOrchestratorSummary
            {
                Configured = false,
                Success = false,
                Message = "UiPath Orchestrator integration is not configured.",
                DeploymentType = options.DeploymentType,
                ErrorCode = "ORCHESTRATOR_NOT_CONFIGURED"
            };
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.RequestTimeout);
            var processes = await ReadCollectionAsync("odata/Releases?$select=Id,Name,ProcessKey,ProcessVersion,IsLatestVersion", ParseProcess, timeout.Token).ConfigureAwait(false);
            var queues = await ReadCollectionAsync("odata/QueueDefinitions?$select=Id,Name,Description,MaxNumberOfRetries", ParseQueue, timeout.Token).ConfigureAwait(false);
            var assets = await ReadCollectionAsync("odata/Assets?$select=Id,Name,ValueScope,ValueType", ParseAsset, timeout.Token).ConfigureAwait(false);
            var machines = await ReadCollectionAsync("odata/Machines?$select=Id,Name,Type", ParseMachine, timeout.Token).ConfigureAwait(false);

            return new UiPathOrchestratorSummary
            {
                Configured = true,
                Success = true,
                Message = "UiPath Orchestrator inventory loaded successfully.",
                DeploymentType = options.DeploymentType,
                Processes = processes,
                Queues = queues,
                Assets = assets,
                Machines = machines
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("ORCHESTRATOR_TIMEOUT", "UiPath Orchestrator did not respond before the configured timeout.");
        }
        catch (HttpRequestException error)
        {
            return Failure("ORCHESTRATOR_UNAVAILABLE", $"UiPath Orchestrator inventory could not be loaded (HTTP {(int?)error.StatusCode ?? 0}).");
        }
        catch (JsonException)
        {
            return Failure("ORCHESTRATOR_INVALID_RESPONSE", "UiPath Orchestrator returned an invalid inventory response.");
        }
    }

    private UiPathOrchestratorSummary Failure(string code, string message) => new()
    {
        Configured = true,
        Success = false,
        Message = message,
        DeploymentType = options.DeploymentType,
        ErrorCode = code
    };

    private async Task<IReadOnlyList<T>> ReadCollectionAsync<T>(string relativeUrl, Func<JsonElement, T> parser, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(options.BaseUri!, relativeUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(options.TenantName)) request.Headers.TryAddWithoutValidation("X-UIPATH-TenantName", options.TenantName);
        if (options.FolderId.HasValue) request.Headers.TryAddWithoutValidation("X-UIPATH-OrganizationUnitId", options.FolderId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException("Orchestrator request failed.", null, response.StatusCode);
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("OData response does not contain a value array.");
        }
        return value.EnumerateArray().Select(parser).ToArray();
    }

    private static UiPathOrchestratorProcess ParseProcess(JsonElement item) => new(
        Long(item, "Id"), Text(item, "Name") ?? Text(item, "ProcessKey") ?? "Unknown",
        Text(item, "ProcessKey"), Text(item, "ProcessVersion"), Bool(item, "IsLatestVersion"));
    private static UiPathOrchestratorQueue ParseQueue(JsonElement item) => new(
        Long(item, "Id"), Text(item, "Name") ?? "Unknown", Text(item, "Description"), Int(item, "MaxNumberOfRetries"));
    private static UiPathOrchestratorAsset ParseAsset(JsonElement item) => new(
        Long(item, "Id"), Text(item, "Name") ?? "Unknown", Text(item, "ValueScope"), Text(item, "ValueType"));
    private static UiPathOrchestratorMachine ParseMachine(JsonElement item) => new(
        Long(item, "Id"), Text(item, "Name") ?? "Unknown", Text(item, "Type"));
    private static string? Text(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static long? Long(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.TryGetInt64(out var result) ? result : null;
    private static int? Int(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : null;
    private static bool? Bool(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
}

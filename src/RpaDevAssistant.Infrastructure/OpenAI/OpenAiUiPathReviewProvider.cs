using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RpaDevAssistant.Core.Ai;

namespace RpaDevAssistant.Infrastructure.OpenAI;

public sealed class OpenAiUiPathReviewProvider : IUiPathAiReviewProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient httpClient;
    private readonly OpenAiReviewOptions options;
    private readonly ILogger<OpenAiUiPathReviewProvider> logger;

    public OpenAiUiPathReviewProvider(
        HttpClient httpClient,
        IOptions<OpenAiReviewOptions> options,
        ILogger<OpenAiUiPathReviewProvider> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;
    }

    public string ProviderName => "OpenAI";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ApiKey);

    public async Task<UiPathAiReviewResult> ReviewAsync(UiPathAiPrompt prompt, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return UiPathAiReviewResult.NotConfigured(prompt.Scope, prompt.WorkflowPath, prompt.Locale);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);

        try
        {
            var result = await SendReviewRequestAsync(prompt, timeout.Token).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                return result;
            }

            if (result.ErrorMessage?.Contains("rate limit", StringComparison.OrdinalIgnoreCase) == true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), timeout.Token).ConfigureAwait(false);
                return await SendReviewRequestAsync(prompt, timeout.Token).ConfigureAwait(false);
            }

            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UiPathAiReviewResult.Failure(prompt.Scope, Localized(prompt.Locale, "AI review timed out.", "AI İnceleme zaman aşımına uğradı."), prompt.WorkflowPath, prompt.Locale);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            logger.LogWarning("OpenAI review request failed without exposing provider payload.");
            return UiPathAiReviewResult.Failure(prompt.Scope, Localized(prompt.Locale, "AI provider request failed.", "AI provider isteği başarısız oldu."), prompt.WorkflowPath, prompt.Locale);
        }
    }

    private async Task<UiPathAiReviewResult> SendReviewRequestAsync(UiPathAiPrompt prompt, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(BuildRequestBody(prompt), JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return UiPathAiReviewResult.Failure(
                prompt.Scope,
                response.StatusCode == HttpStatusCode.Unauthorized
                    ? Localized(prompt.Locale, "AI provider authorization failed.", "AI provider authorization başarısız oldu.")
                    : response.StatusCode == (HttpStatusCode)429
                        ? Localized(prompt.Locale, "AI provider rate limit was reached.", "AI provider rate limit değerine ulaşıldı.")
                        : Localized(prompt.Locale, "AI provider request failed.", "AI provider isteği başarısız oldu."),
                prompt.WorkflowPath,
                prompt.Locale);
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseResponsePayload(payload, prompt);
    }

    public static UiPathAiReviewResult ParseResponsePayload(string payload, UiPathAiPrompt prompt)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var text = ExtractOutputText(document.RootElement);
            if (string.IsNullOrWhiteSpace(text))
            {
                return UiPathAiReviewResult.Failure(prompt.Scope, Localized(prompt.Locale, "AI provider returned an empty structured response.", "AI provider boş structured response döndürdü."), prompt.WorkflowPath, prompt.Locale);
            }

            var result = JsonSerializer.Deserialize<UiPathAiReviewResult>(text, JsonOptions);
            return result is null
                ? UiPathAiReviewResult.Failure(prompt.Scope, Localized(prompt.Locale, "AI provider returned an invalid structured response.", "AI provider geçersiz structured response döndürdü."), prompt.WorkflowPath, prompt.Locale)
                : result with
                {
                    IsConfigured = true,
                    IsSuccess = true,
                    Model = ReadString(document.RootElement, "model") ?? result.Model,
                    ReviewedScope = prompt.Scope,
                    ReviewedWorkflowPath = prompt.WorkflowPath,
                    InputTokens = ReadInt(document.RootElement, "usage", "input_tokens"),
                    OutputTokens = ReadInt(document.RootElement, "usage", "output_tokens"),
                    TotalTokens = ReadInt(document.RootElement, "usage", "total_tokens")
                };
        }
        catch (JsonException)
        {
            return UiPathAiReviewResult.Failure(prompt.Scope, Localized(prompt.Locale, "AI provider returned invalid JSON.", "AI provider geçersiz JSON döndürdü."), prompt.WorkflowPath, prompt.Locale);
        }
    }

    private static string Localized(string? locale, string english, string turkish)
    {
        return string.Equals(locale, "tr", StringComparison.OrdinalIgnoreCase) || string.Equals(locale, "tr-TR", StringComparison.OrdinalIgnoreCase)
            ? turkish
            : english;
    }

    private object BuildRequestBody(UiPathAiPrompt prompt)
    {
        return new
        {
            model = options.Model,
            instructions = prompt.SystemInstructions,
            input = prompt.UserContext,
            max_output_tokens = options.MaxOutputTokens,
            temperature = options.Temperature,
            text = new
            {
                format = new
                {
                    type = "json_object"
                }
            }
        };
    }

    private static string? ExtractOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString();
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement element, params string[] path)
    {
        return TryRead(element, out var current, path) && current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static int? ReadInt(JsonElement element, params string[] path)
    {
        return TryRead(element, out var current, path) && current.ValueKind == JsonValueKind.Number && current.TryGetInt32(out var value) ? value : null;
    }

    private static bool TryRead(JsonElement element, out JsonElement current, params string[] path)
    {
        current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return false;
            }
        }

        return true;
    }
}

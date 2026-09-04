using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RpaDevAssistant.Core.ProjectAssistant;

namespace RpaDevAssistant.Infrastructure.OpenAI;

public sealed class OpenAiUiPathProjectAssistantProvider : IUiPathProjectAssistantAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient httpClient;
    private readonly OpenAiReviewOptions options;
    private readonly ILogger<OpenAiUiPathProjectAssistantProvider> logger;

    public OpenAiUiPathProjectAssistantProvider(
        HttpClient httpClient,
        IOptions<OpenAiReviewOptions> options,
        ILogger<OpenAiUiPathProjectAssistantProvider> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;
    }

    public string ProviderName => "OpenAI";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ApiKey);

    public async Task<UiPathProjectAnswer> AnswerAsync(UiPathProjectAssistantPrompt prompt, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return NotConfigured(prompt.Locale);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(BuildRequestBody(prompt), JsonOptions), Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Failed(response.StatusCode == HttpStatusCode.Unauthorized
                    ? Localized(prompt.Locale, "AI provider authorization failed.", "AI provider authorization başarısız oldu.")
                    : response.StatusCode == (HttpStatusCode)429
                        ? Localized(prompt.Locale, "AI provider rate limit was reached.", "AI provider rate limit değerine ulaşıldı.")
                        : Localized(prompt.Locale, "AI provider request failed.", "AI provider isteği başarısız oldu."));
            }

            var payload = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            return ParseResponsePayload(payload);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(Localized(prompt.Locale, "AI project assistant timed out.", "Ask Project AI isteği zaman aşımına uğradı."));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            logger.LogWarning("OpenAI project assistant request failed without exposing provider payload.");
            return Failed(Localized(prompt.Locale, "AI provider request failed.", "AI provider isteği başarısız oldu."));
        }
    }

    public static UiPathProjectAnswer ParseResponsePayload(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var text = ExtractOutputText(document.RootElement);
            if (string.IsNullOrWhiteSpace(text))
            {
                return Failed("AI provider returned an empty structured response.");
            }

            var result = JsonSerializer.Deserialize<UiPathProjectAnswer>(text, JsonOptions);
            return result is null
                ? Failed("AI provider returned an invalid structured response.")
                : result with
                {
                    UsedAi = true,
                    Model = ReadString(document.RootElement, "model") ?? result.Model,
                    GeneratedAtUtc = result.GeneratedAtUtc == default ? DateTimeOffset.UtcNow : result.GeneratedAtUtc
                };
        }
        catch (JsonException)
        {
            return Failed("AI provider returned invalid JSON.");
        }
    }

    private object BuildRequestBody(UiPathProjectAssistantPrompt prompt)
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

    private static UiPathProjectAnswer NotConfigured(string? locale)
    {
        return Failed(Localized(locale, "AI Review is not configured. Set OPENAI_API_KEY to enable interpretation questions.", "AI İnceleme yapılandırılmamış. Yorum sorularını etkinleştirmek için OPENAI_API_KEY ayarlayın."));
    }

    private static UiPathProjectAnswer Failed(string message)
    {
        return new UiPathProjectAnswer
        {
            Answer = message,
            AnswerType = UiPathProjectAnswerType.InsufficientEvidence,
            Confidence = UiPathProjectAnswerConfidence.Low,
            UsedAi = false,
            ErrorMessage = message
        };
    }

    private static string Localized(string? locale, string english, string turkish)
    {
        return string.Equals(locale, "tr", StringComparison.OrdinalIgnoreCase) || string.Equals(locale, "tr-TR", StringComparison.OrdinalIgnoreCase)
            ? turkish
            : english;
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
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}

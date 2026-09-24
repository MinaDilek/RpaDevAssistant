using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RpaDevAssistant.Core.Fixes;

namespace RpaDevAssistant.Infrastructure.OpenAI;

public sealed class OpenAiUiPathFixAdvisor : IUiPathAiFixAdvisor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient httpClient;
    private readonly OpenAiReviewOptions options;
    private readonly ILogger<OpenAiUiPathFixAdvisor> logger;

    public OpenAiUiPathFixAdvisor(HttpClient httpClient, IOptions<OpenAiReviewOptions> options, ILogger<OpenAiUiPathFixAdvisor> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;
    }

    public string ProviderName => options.TryResolveProvider(out var configuration)
        ? configuration.ProviderName
        : options.Provider.ToString();

    public bool IsConfigured => options.TryResolveProvider(out _);

    public async Task<UiPathFixSuggestion> SuggestAsync(UiPathAiFixPrompt prompt, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return Failed(Localized(prompt.Locale, "AI-assisted fix suggestions are not configured.", "AI-assisted fix suggestions yapılandırılmamış."));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);

        try
        {
            if (!options.TryResolveProvider(out var configuration))
            {
                return Failed(Localized(prompt.Locale, "AI-assisted fix suggestions are not configured.", "AI-assisted fix suggestions yapılandırılmamış."));
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, configuration.Endpoint);
            if (configuration.ApiKey is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.ApiKey);
            }
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
            return Failed(Localized(prompt.Locale, "AI-assisted fix suggestion timed out.", "AI-assisted fix suggestion zaman aşımına uğradı."));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            logger.LogWarning("OpenAI fix suggestion request failed without exposing provider payload.");
            return Failed(Localized(prompt.Locale, "AI provider request failed.", "AI provider isteği başarısız oldu."));
        }
    }

    public static UiPathFixSuggestion ParseResponsePayload(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var text = ExtractOutputText(document.RootElement);
            if (string.IsNullOrWhiteSpace(text))
            {
                return Failed("AI provider returned an empty structured response.");
            }

            var suggestion = JsonSerializer.Deserialize<UiPathFixSuggestion>(text, JsonOptions);
            return suggestion is null
                ? Failed("AI provider returned an invalid structured response.")
                : suggestion with
                {
                    RequiresAi = true,
                    CanAutoApply = false,
                    GeneratedAtUtc = suggestion.GeneratedAtUtc == default ? DateTimeOffset.UtcNow : suggestion.GeneratedAtUtc
                };
        }
        catch (JsonException)
        {
            return Failed("AI provider returned invalid JSON.");
        }
    }

    private object BuildRequestBody(UiPathAiFixPrompt prompt)
    {
        return new
        {
            model = options.EffectiveModel,
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

    private static UiPathFixSuggestion Failed(string message)
    {
        return new UiPathFixSuggestion
        {
            Id = "ai-fix-unavailable",
            RuleId = "Unknown",
            Title = "AI-assisted fix unavailable",
            Description = message,
            FixType = UiPathFixSuggestionType.WorkflowRefactor,
            Confidence = UiPathFixConfidence.Low,
            RiskLevel = UiPathFixRiskLevel.High,
            Explanation = message,
            ValidationNotes = [message],
            RequiresAi = true,
            CanAutoApply = false,
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
}

using System.Net;

namespace RpaDevAssistant.Infrastructure.OpenAI;

public enum AiProviderKind
{
    OpenAI,
    Local
}

public sealed class OpenAiReviewOptions
{
    private static readonly Uri OpenAiResponsesEndpoint = new("https://api.openai.com/v1/responses");

    public AiProviderKind Provider { get; set; } = AiProviderKind.OpenAI;

    public string? ApiKey { get; set; }

    public string? LocalEndpoint { get; set; }

    public string? LocalApiKey { get; set; }

    public string? LocalModel { get; set; }

    public string Model { get; set; } = "gpt-5.6-mini";

    public string EffectiveModel => Provider == AiProviderKind.Local && !string.IsNullOrWhiteSpace(LocalModel)
        ? LocalModel
        : Model;

    public int MaxOutputTokens { get; set; } = 1200;

    public double Temperature { get; set; } = 0.2;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    public bool TryResolveProvider(out AiProviderRequestConfiguration configuration)
    {
        if (Provider == AiProviderKind.OpenAI)
        {
            configuration = new AiProviderRequestConfiguration(
                "OpenAI",
                OpenAiResponsesEndpoint,
                string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey);
            return configuration.ApiKey is not null;
        }

        if (!TryResolveLoopbackEndpoint(LocalEndpoint, out var endpoint))
        {
            configuration = default;
            return false;
        }

        configuration = new AiProviderRequestConfiguration(
            "Local OpenAI-compatible",
            endpoint,
            string.IsNullOrWhiteSpace(LocalApiKey) ? null : LocalApiKey);
        return true;
    }

    internal static bool TryResolveLoopbackEndpoint(string? value, out Uri endpoint)
    {
        endpoint = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate)
            || candidate.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(candidate.UserInfo)
            || !string.IsNullOrEmpty(candidate.Query)
            || !string.IsNullOrEmpty(candidate.Fragment)
            || !IsLoopbackHost(candidate.Host))
        {
            return false;
        }

        endpoint = candidate;
        return true;
    }

    private static bool IsLoopbackHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }
}

public readonly record struct AiProviderRequestConfiguration(string ProviderName, Uri Endpoint, string? ApiKey);

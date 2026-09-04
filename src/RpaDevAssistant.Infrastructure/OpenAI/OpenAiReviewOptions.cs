namespace RpaDevAssistant.Infrastructure.OpenAI;

public sealed class OpenAiReviewOptions
{
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gpt-5.6-mini";

    public int MaxOutputTokens { get; set; } = 1200;

    public double Temperature { get; set; } = 0.2;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
}

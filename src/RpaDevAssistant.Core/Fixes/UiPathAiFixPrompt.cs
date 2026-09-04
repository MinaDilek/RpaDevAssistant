namespace RpaDevAssistant.Core.Fixes;

public sealed record UiPathAiFixPrompt
{
    public required string SystemInstructions { get; init; }

    public required string UserContext { get; init; }

    public string? Locale { get; init; }
}

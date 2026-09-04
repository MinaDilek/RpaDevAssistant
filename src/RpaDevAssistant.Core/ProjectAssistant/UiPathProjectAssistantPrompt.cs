namespace RpaDevAssistant.Core.ProjectAssistant;

public sealed record UiPathProjectAssistantPrompt
{
    public required string SystemInstructions { get; init; }

    public required string UserContext { get; init; }

    public required string Question { get; init; }

    public string? Locale { get; init; }
}

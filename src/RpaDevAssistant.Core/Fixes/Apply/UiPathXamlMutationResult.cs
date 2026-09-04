namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathXamlMutationResult
{
    public bool Success { get; init; }

    public required string Message { get; init; }

    public string? PreviousValue { get; init; }

    public string? NewValue { get; init; }

    public string? MutatedContent { get; init; }

    public string? ErrorCode { get; init; }
}

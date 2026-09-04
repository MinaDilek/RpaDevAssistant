namespace RpaDevAssistant.Core.Ai;

public sealed record UiPathAiWorkflowContext
{
    public required string RelativePath { get; init; }

    public int ActivityCount { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public IReadOnlyList<string> Activities { get; init; } = [];

    public IReadOnlyList<string> InvokeReferences { get; init; } = [];

    public bool IsTruncated { get; init; }
}

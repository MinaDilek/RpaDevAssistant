namespace RpaDevAssistant.Core.Ai;

public sealed record UiPathAiReviewOptions
{
    public int MaxActivitiesPerWorkflow { get; init; } = 80;

    public int MaxFindings { get; init; } = 40;

    public int MaxWorkflows { get; init; } = 25;

    public int MaxPropertyLength { get; init; } = 120;
}

using RpaDevAssistant.Core.Ai;

namespace RpaDevAssistant.Api.Requests;

public sealed record AiReviewUiPathProjectRequest
{
    public string? ProjectPath { get; init; }

    public string? ProfileId { get; init; }

    public UiPathAiReviewScope Scope { get; init; } = UiPathAiReviewScope.Project;

    public string? WorkflowPath { get; init; }

    public string? AdditionalInstructions { get; init; }

    public string? Locale { get; init; }
}

namespace RpaDevAssistant.Core.Ai;

public sealed record UiPathAiReviewResult
{
    public bool IsConfigured { get; init; } = true;

    public bool IsSuccess { get; init; } = true;

    public string? ErrorMessage { get; init; }

    public required string Summary { get; init; }

    public IReadOnlyList<UiPathAiReviewEvidence> Evidence { get; init; } = [];

    public string? Interpretation { get; init; }

    public UiPathAiRiskLevel RiskLevel { get; init; }

    public IReadOnlyList<string> Strengths { get; init; } = [];

    public IReadOnlyList<UiPathAiReviewIssue> Issues { get; init; } = [];

    public IReadOnlyList<string> Recommendations { get; init; } = [];

    public IReadOnlyList<string> ArchitectureObservations { get; init; } = [];

    public double Confidence { get; init; }

    public UiPathAiReviewScope ReviewedScope { get; init; }

    public string? ReviewedWorkflowPath { get; init; }

    public string? Model { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public int? TotalTokens { get; init; }

    public static UiPathAiReviewResult NotConfigured(UiPathAiReviewScope scope, string? workflowPath = null, string? locale = null)
    {
        var isTr = string.Equals(locale, "tr", StringComparison.OrdinalIgnoreCase) || string.Equals(locale, "tr-TR", StringComparison.OrdinalIgnoreCase);
        return new UiPathAiReviewResult
        {
            IsConfigured = false,
            IsSuccess = false,
            Summary = isTr ? "AI İnceleme yapılandırılmamış." : "AI Review is not configured.",
            ErrorMessage = isTr ? "AI İnceleme yapılandırılmamış." : "AI Review is not configured.",
            ReviewedScope = scope,
            ReviewedWorkflowPath = workflowPath,
            RiskLevel = UiPathAiRiskLevel.Low,
            Confidence = 0
        };
    }

    public static UiPathAiReviewResult Failure(UiPathAiReviewScope scope, string message, string? workflowPath = null, string? locale = null)
    {
        var isTr = string.Equals(locale, "tr", StringComparison.OrdinalIgnoreCase) || string.Equals(locale, "tr-TR", StringComparison.OrdinalIgnoreCase);
        return new UiPathAiReviewResult
        {
            IsSuccess = false,
            Summary = isTr ? "AI İnceleme tamamlanamadı." : "AI review could not be completed.",
            ErrorMessage = message,
            ReviewedScope = scope,
            ReviewedWorkflowPath = workflowPath,
            RiskLevel = UiPathAiRiskLevel.Low,
            Confidence = 0
        };
    }
}

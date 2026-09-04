namespace RpaDevAssistant.Core.Analysis;

public sealed record UiPathAnalysisThresholds
{
    public static UiPathAnalysisThresholds Default { get; } = new();

    public int LongDelaySeconds { get; init; } = 5;

    public int ExcessiveTimeoutMs { get; init; } = 120_000;

    public int LowTimeoutMs { get; init; } = 1_000;

    public int ContinueOnErrorWorkflowThreshold { get; init; } = 3;

    public int SelectorLengthThreshold { get; init; } = 500;

    public int WorkflowArgumentThreshold { get; init; } = 10;

    public int LargeWorkflowActivityThreshold { get; init; } = 100;

    public int LargeWorkflowDepthThreshold { get; init; } = 12;

    public int ComplexityMediumThreshold { get; init; } = 40;

    public int ComplexityHighThreshold { get; init; } = 90;

    public int ComplexityVeryHighThreshold { get; init; } = 160;
}

namespace RpaDevAssistant.Core.Analysis;

public sealed record UiPathAnalysisPerformanceMetrics
{
    public double TotalElapsedMilliseconds { get; init; }

    public double ScanElapsedMilliseconds { get; init; }

    public double WorkflowDiscoveryElapsedMilliseconds { get; init; }

    public double XamlParsingElapsedMilliseconds { get; init; }

    public double RuleAnalysisElapsedMilliseconds { get; init; }

    public double ScoringElapsedMilliseconds { get; init; }
}

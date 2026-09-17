namespace RpaDevAssistant.Core.Models;

public sealed record UiPathScanPerformanceMetrics
{
    public double TotalElapsedMilliseconds { get; init; }

    public double WorkflowDiscoveryElapsedMilliseconds { get; init; }

    public double XamlParsingElapsedMilliseconds { get; init; }
}

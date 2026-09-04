namespace RpaDevAssistant.Core.Reporting;

public sealed record UiPathReportWorkflow
{
    public required string RelativePath { get; init; }

    public int ActivityCount { get; init; }

    public int FindingCount { get; init; }

    public int CriticalCount { get; init; }

    public int ErrorCount { get; init; }

    public int WarningCount { get; init; }

    public int SuggestionCount { get; init; }

    public int InfoCount { get; init; }

    public UiPathReportWorkflowComplexity? Complexity { get; init; }

    public required string Status { get; init; }
}

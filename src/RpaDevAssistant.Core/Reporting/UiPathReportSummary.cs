namespace RpaDevAssistant.Core.Reporting;

public sealed record UiPathReportSummary
{
    public required UiPathExecutiveSummary ExecutiveSummary { get; init; }

    public int TotalFindings { get; init; }

    public int CriticalCount { get; init; }

    public int ErrorCount { get; init; }

    public int WarningCount { get; init; }

    public int SuggestionCount { get; init; }

    public int InfoCount { get; init; }

    public int WorkflowsWithFindings { get; init; }

    public int CleanWorkflows { get; init; }

    public IReadOnlyList<UiPathReportCount> TopCategories { get; init; } = [];

    public IReadOnlyList<UiPathReportCount> TopRules { get; init; } = [];
}

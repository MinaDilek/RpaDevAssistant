namespace RpaDevAssistant.Core.Analysis;

public sealed class UiPathStaticAnalysisResult
{
    public int TotalFindings => Findings.Count;

    public int TotalOccurrences => Findings.Sum(finding => Math.Max(1, finding.OccurrenceCount));

    public int CriticalCount => CountBySeverity(RuleSeverity.Critical);

    public int ErrorCount => CountBySeverity(RuleSeverity.Error);

    public int WarningCount => CountBySeverity(RuleSeverity.Warning);

    public int SuggestionCount => CountBySeverity(RuleSeverity.Suggestion);

    public int InfoCount => CountBySeverity(RuleSeverity.Info);

    public List<UiPathAnalysisFinding> Findings { get; } = [];

    private int CountBySeverity(RuleSeverity severity)
    {
        return Findings.Count(finding => finding.Severity == severity);
    }
}

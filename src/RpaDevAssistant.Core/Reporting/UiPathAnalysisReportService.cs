using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.Reporting;

public sealed class UiPathAnalysisReportService : IUiPathAnalysisReportService
{
    private readonly IUiPathProjectAnalyzer analyzer;
    private readonly IUiPathAnalysisReportBuilder reportBuilder;

    public UiPathAnalysisReportService(IUiPathProjectAnalyzer analyzer, IUiPathAnalysisReportBuilder reportBuilder)
    {
        this.analyzer = analyzer;
        this.reportBuilder = reportBuilder;
    }

    public UiPathAnalysisReport Generate(string projectPath, string? profileId = null)
    {
        var result = analyzer.Analyze(projectPath, profileId);
        return reportBuilder.Build(result.ProjectScan, result.Analysis, result.QualityScore, result.Profile);
    }
}

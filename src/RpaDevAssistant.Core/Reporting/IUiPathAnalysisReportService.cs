namespace RpaDevAssistant.Core.Reporting;

public interface IUiPathAnalysisReportService
{
    UiPathAnalysisReport Generate(string projectPath, string? profileId = null);
}

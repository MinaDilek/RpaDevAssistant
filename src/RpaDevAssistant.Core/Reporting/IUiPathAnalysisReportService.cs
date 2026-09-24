namespace RpaDevAssistant.Core.Reporting;

public interface IUiPathAnalysisReportService
{
    UiPathAnalysisReport Generate(string projectPath, string? profileId = null);

    Task<UiPathAnalysisReport> GenerateAsync(UiPathReportGenerationOptions options, CancellationToken cancellationToken = default);
}

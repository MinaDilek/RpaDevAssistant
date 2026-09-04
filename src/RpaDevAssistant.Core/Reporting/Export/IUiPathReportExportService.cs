namespace RpaDevAssistant.Core.Reporting.Export;

public interface IUiPathReportExportService
{
    UiPathReportExportResult Export(UiPathAnalysisReport report, UiPathReportExportFormat format, string? locale = null);
}

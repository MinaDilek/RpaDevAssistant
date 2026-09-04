namespace RpaDevAssistant.Core.Reporting.Export;

public interface IUiPathReportExporter
{
    UiPathReportExportFormat Format { get; }

    UiPathReportExportResult Export(UiPathAnalysisReport report, string? locale = null);
}

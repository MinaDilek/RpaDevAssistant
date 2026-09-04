namespace RpaDevAssistant.Core.Reporting.Export;

public sealed class UiPathReportExportService : IUiPathReportExportService
{
    private readonly IReadOnlyDictionary<UiPathReportExportFormat, IUiPathReportExporter> exporters;

    public UiPathReportExportService(IEnumerable<IUiPathReportExporter> exporters)
    {
        this.exporters = exporters.ToDictionary(exporter => exporter.Format);
    }

    public UiPathReportExportResult Export(UiPathAnalysisReport report, UiPathReportExportFormat format, string? locale = null)
    {
        if (!exporters.TryGetValue(format, out var exporter))
        {
            throw new NotSupportedException($"Report export format '{format}' is not supported.");
        }

        return exporter.Export(report, locale);
    }
}

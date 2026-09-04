using System.Text.Json;
using System.Text.Json.Serialization;

namespace RpaDevAssistant.Core.Reporting.Export;

public sealed class JsonUiPathReportExporter : IUiPathReportExporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public UiPathReportExportFormat Format => UiPathReportExportFormat.Json;

    public UiPathReportExportResult Export(UiPathAnalysisReport report, string? locale = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new UiPathReportExportResult
        {
            FileName = UiPathReportFileNameGenerator.Generate(report.ProjectName, report.GeneratedAtUtc, Format),
            ContentType = "application/json; charset=utf-8",
            Content = JsonSerializer.Serialize(report, SerializerOptions)
        };
    }
}

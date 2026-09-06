using System.Globalization;
using System.Text;

namespace RpaDevAssistant.Core.Reporting.Export;

public sealed class PdfUiPathReportExporter : IUiPathReportExporter
{
    public UiPathReportExportFormat Format => UiPathReportExportFormat.Pdf;

    public UiPathReportExportResult Export(UiPathAnalysisReport report, string? locale = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new UiPathReportExportResult
        {
            FileName = UiPathReportFileNameGenerator.Generate(report.ProjectName, report.GeneratedAtUtc, Format),
            ContentType = "application/pdf",
            Content = BuildPdf(report)
        };
    }

    private static string BuildPdf(UiPathAnalysisReport report)
    {
        var lines = BuildLines(report);
        var pages = lines.Chunk(42).Select(BuildPageStream).ToArray();
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pages.Length).Select(index => $"{3 + (index * 2)} 0 R"))}] /Count {pages.Length} >>"
        };

        for (var index = 0; index < pages.Length; index++)
        {
            var pageObjectNumber = 3 + (index * 2);
            var contentObjectNumber = pageObjectNumber + 1;
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /Contents {contentObjectNumber} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(pages[index])} >>\nstream\n{pages[index]}\nendstream");
        }

        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(CultureInfo.InvariantCulture, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n");
        builder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            builder.Append(CultureInfo.InvariantCulture, $"{offset:0000000000} 00000 n \n");
        }

        builder.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return builder.ToString();
    }

    private static IReadOnlyList<string> BuildLines(UiPathAnalysisReport report)
    {
        var lines = new List<string>
        {
            "RPA Dev Assistant Analysis Report",
            $"Generated: {report.GeneratedAtUtc:u}",
            $"Project: {report.ProjectName ?? "UiPath Project"}",
            $"Path: {report.ProjectPath}",
            $"Score: {report.QualityScore} / {report.Grade}",
            $"Workflows: {report.WorkflowCount}",
            $"Activities: {report.TotalActivityCount}",
            $"Findings: {report.Summary.TotalFindings} (Critical {report.Summary.CriticalCount}, Error {report.Summary.ErrorCount}, Warning {report.Summary.WarningCount}, Suggestion {report.Summary.SuggestionCount}, Info {report.Summary.InfoCount})",
            string.Empty,
            "Top Rules"
        };

        lines.AddRange(report.Summary.TopRules.Take(10).Select(item => $"{item.Name}: {item.Count}"));
        lines.Add(string.Empty);
        lines.Add("Findings");
        lines.AddRange(report.Findings.Take(80).Select(finding => $"{finding.RuleId} {finding.Severity} {finding.WorkflowPath ?? "-"} - {finding.Message ?? finding.RuleName}"));

        if (report.Findings.Count > 80)
        {
            lines.Add($"... {report.Findings.Count - 80} more findings omitted from PDF summary.");
        }

        return lines.Select(ToPdfSafeText).ToArray();
    }

    private static string BuildPageStream(IEnumerable<string> lines)
    {
        var builder = new StringBuilder();
        builder.Append("BT\n/F1 10 Tf\n50 750 Td\n14 TL\n");
        foreach (var line in lines)
        {
            builder.Append('(').Append(EscapePdfString(line)).Append(") Tj\nT*\n");
        }

        builder.Append("ET");
        return builder.ToString();
    }

    private static string EscapePdfString(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static string ToPdfSafeText(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character is >= ' ' and <= '~' ? character : '?');
        }

        return builder.ToString();
    }
}

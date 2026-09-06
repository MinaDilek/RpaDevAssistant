using System.Text;

namespace RpaDevAssistant.Core.Reporting.Export;

public static class UiPathReportFileNameGenerator
{
    private static readonly char[] InvalidFileNameCharacters =
    [
        '<',
        '>',
        ':',
        '"',
        '/',
        '\\',
        '|',
        '?',
        '*'
    ];

    public static string Generate(string? projectName, DateTimeOffset generatedAtUtc, UiPathReportExportFormat format)
    {
        var safeProjectName = Sanitize(projectName);
        var timestamp = generatedAtUtc.ToString("yyyyMMdd-HHmmss");
        var extension = format switch
        {
            UiPathReportExportFormat.Html => "html",
            UiPathReportExportFormat.Pdf => "pdf",
            _ => "json"
        };
        return $"{safeProjectName}-RPA-Analysis-{timestamp}.{extension}";
    }

    public static string Sanitize(string? fileNamePart)
    {
        if (string.IsNullOrWhiteSpace(fileNamePart))
        {
            return "UiPathProject";
        }

        var builder = new StringBuilder();
        foreach (var character in fileNamePart.Trim())
        {
            builder.Append(InvalidFileNameCharacters.Contains(character) || char.IsControl(character) ? '-' : character);
        }

        var sanitized = string.Join('-', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        sanitized = sanitized.Trim('-', '.');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "UiPathProject";
        }

        return sanitized.Length <= 80 ? sanitized : sanitized[..80].Trim('-', '.');
    }
}

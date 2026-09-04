namespace RpaDevAssistant.Core.Reporting.Export;

public sealed record UiPathReportExportResult
{
    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required string Content { get; init; }
}

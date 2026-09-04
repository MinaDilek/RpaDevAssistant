namespace RpaDevAssistant.Core.Reporting;

public sealed record UiPathReportCount
{
    public required string Name { get; init; }

    public int Count { get; init; }
}

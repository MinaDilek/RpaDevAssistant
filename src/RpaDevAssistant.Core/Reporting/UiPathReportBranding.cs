namespace RpaDevAssistant.Core.Reporting;

public sealed record UiPathReportBranding
{
    public string? CompanyName { get; init; }

    public string? LogoDataUri { get; init; }

    public string? AccentColor { get; init; }
}

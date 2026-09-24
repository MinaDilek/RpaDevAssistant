namespace RpaDevAssistant.Api.Requests;

using RpaDevAssistant.Core.Reporting;

public sealed record ReportUiPathProjectRequest
{
    public string? ProjectPath { get; init; }

    public string? ProfileId { get; init; }

    public string? Format { get; init; }

    public string? Locale { get; init; }

    public bool IncludeComparison { get; init; }

    public string? BaselineSnapshotId { get; init; }

    public string? TargetSnapshotId { get; init; }

    public bool IncludeAiReview { get; init; }

    public bool IncludeFixSuggestions { get; init; }

    public int? MaxFixSuggestions { get; init; }

    public UiPathReportBranding? Branding { get; init; }
}

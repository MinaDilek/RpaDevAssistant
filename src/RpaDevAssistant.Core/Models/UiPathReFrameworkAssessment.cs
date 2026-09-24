namespace RpaDevAssistant.Core.Models;

public sealed record UiPathReFrameworkAssessment
{
    public bool IsDetected { get; init; }

    public string DetectionStatus { get; init; } = "NotDetected";

    public int PassedCount => ChecklistItems.Count(item => item.Status == "Pass");

    public int FailedCount => ChecklistItems.Count(item => item.Status == "Fail");

    public int UnknownCount => ChecklistItems.Count(item => item.Status == "Unknown");

    public IReadOnlyList<string> DetectedCoreWorkflows { get; init; } = [];

    public IReadOnlyList<string> MissingCoreWorkflows { get; init; } = [];

    public IReadOnlyList<UiPathReFrameworkChecklistItem> ChecklistItems { get; init; } = [];
}

public sealed record UiPathReFrameworkChecklistItem
{
    public required string Id { get; init; }

    public required string Status { get; init; }

    public IReadOnlyList<string> Evidence { get; init; } = [];
}

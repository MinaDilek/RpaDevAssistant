namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathBackupDetail
{
    public UiPathBackupSummary? Summary { get; init; }

    public UiPathBackupMetadata? Metadata { get; init; }

    public bool Found => Summary is not null;
}

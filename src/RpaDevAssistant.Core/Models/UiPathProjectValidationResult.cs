namespace RpaDevAssistant.Core.Models;

public sealed record UiPathProjectValidationResult
{
    public required string ProjectPath { get; init; }

    public bool FolderExists { get; init; }

    public bool ProjectJsonExists { get; init; }

    public bool LooksLikeUiPathProject => FolderExists && ProjectJsonExists;

    public IReadOnlyList<string> Messages { get; init; } = [];
}

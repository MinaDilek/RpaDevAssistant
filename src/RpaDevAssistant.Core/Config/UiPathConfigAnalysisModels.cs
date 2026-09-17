using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Config;

public sealed record UiPathConfigAnalysisRequest
{
    public required string ProjectPath { get; init; }

    public string? ConfigPath { get; init; }
}

public record UiPathConfigChangePreviewRequest
{
    public required string ProjectPath { get; init; }

    public string? ConfigPath { get; init; }

    public IReadOnlyList<string> RemoveKeys { get; init; } = [];

    public IReadOnlyList<UiPathConfigAdditionRequest> Additions { get; init; } = [];
}

public sealed record UiPathConfigGenerateRequest : UiPathConfigChangePreviewRequest
{
    public required string OutputPath { get; init; }
}

public sealed record UiPathConfigAdditionRequest
{
    public required string Key { get; init; }

    public required string Value { get; init; }

    public string? Description { get; init; }

    public string? Source { get; init; }
}

public sealed record UiPathConfigAnalysisResult
{
    public required string ProjectPath { get; init; }

    public string? ProjectName { get; init; }

    public string? ConfigPath { get; init; }

    public bool ConfigFound { get; init; }

    public bool CanGenerate { get; init; }

    public IReadOnlyList<string> Messages { get; init; } = [];

    public IReadOnlyList<UiPathConfigEntry> Entries { get; init; } = [];

    public IReadOnlyList<UiPathConfigUsage> Usages { get; init; } = [];

    public IReadOnlyList<UiPathUnusedConfigKey> UnusedKeys { get; init; } = [];

    public IReadOnlyList<UiPathMissingConfigKey> MissingKeys { get; init; } = [];

    public IReadOnlyList<UiPathHardCodedConfigCandidate> HardCodedCandidates { get; init; } = [];

    public UiPathConfigAnalysisOverview Overview { get; init; } = new();
}

public sealed record UiPathConfigAnalysisOverview
{
    public int ConfigKeyCount { get; init; }

    public int UsedKeyCount { get; init; }

    public int UnusedKeyCount { get; init; }

    public int MissingKeyCount { get; init; }

    public int HardCodedCandidateCount { get; init; }

    public int SensitiveCandidateCount { get; init; }
}

public sealed record UiPathConfigEntry
{
    public required string Key { get; init; }

    public string? Value { get; init; }

    public string? Description { get; init; }

    public required string WorkbookPath { get; init; }

    public required string SheetName { get; init; }

    public int RowNumber { get; init; }
}

public sealed record UiPathConfigUsage
{
    public required string Key { get; init; }

    public required string WorkflowPath { get; init; }

    public string? ActivityId { get; init; }

    public string? ActivityName { get; init; }

    public string? ActivityDisplayName { get; init; }

    public string? PropertyName { get; init; }
}

public sealed record UiPathUnusedConfigKey
{
    public required UiPathConfigEntry Entry { get; init; }
}

public sealed record UiPathMissingConfigKey
{
    public required string Key { get; init; }

    public IReadOnlyList<UiPathConfigUsage> References { get; init; } = [];

    public string SuggestedValue { get; init; } = "";

    public bool IsPreselected { get; init; }
}

public sealed record UiPathHardCodedConfigCandidate
{
    public required string Id { get; init; }

    public required UiPathHardCodedConfigCandidateType Type { get; init; }

    public required string DisplayValue { get; init; }

    public required string SuggestedKey { get; init; }

    public required string Recommendation { get; init; }

    public bool IsSensitive { get; init; }

    public bool CanAddToConfig { get; init; }

    public int OccurrenceCount { get; init; }

    public IReadOnlyList<UiPathConfigUsage> Occurrences { get; init; } = [];
}

public enum UiPathHardCodedConfigCandidateType
{
    Url,
    AbsolutePath,
    Email,
    QueueName,
    AssetName,
    ApiEndpoint,
    TimeoutOrRetry,
    StaticSelector,
    SensitiveValue
}

public sealed record UiPathConfigChangePreview
{
    public required string ProjectPath { get; init; }

    public string? ConfigPath { get; init; }

    public bool IsValid { get; init; }

    public IReadOnlyList<string> ValidationMessages { get; init; } = [];

    public IReadOnlyList<UiPathConfigPreviewChange> Changes { get; init; } = [];
}

public sealed record UiPathConfigPreviewChange
{
    public required UiPathConfigPreviewChangeType ChangeType { get; init; }

    public required string Key { get; init; }

    public string? BeforeValue { get; init; }

    public string? AfterValue { get; init; }

    public string? SheetName { get; init; }

    public int? RowNumber { get; init; }
}

public enum UiPathConfigPreviewChangeType
{
    Remove,
    Add,
    Keep
}

public sealed record UiPathConfigGenerateResult
{
    public bool Success { get; init; }

    public bool Generated { get; init; }

    public required string Message { get; init; }

    public string? OutputPath { get; init; }

    public UiPathConfigChangePreview? Preview { get; init; }
}

public interface IUiPathConfigAnalysisService
{
    UiPathConfigAnalysisResult Analyze(string projectPath, string? configPath = null);

    UiPathConfigChangePreview PreviewChanges(UiPathConfigChangePreviewRequest request);

    UiPathConfigGenerateResult Generate(UiPathConfigGenerateRequest request);
}

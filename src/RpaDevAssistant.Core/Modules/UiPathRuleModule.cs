using RpaDevAssistant.Core.Analysis.CustomRules;
using RpaDevAssistant.Core.Analysis.Profiles;

namespace RpaDevAssistant.Core.Modules;

public sealed record UiPathRuleModule
{
    public int SchemaVersion { get; init; } = 1;
    public required string ModuleId { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Publisher { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset ExportedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<UiPathCustomRuleDefinition> Rules { get; init; } = [];
    public IReadOnlyList<UiPathRuleProfile> Profiles { get; init; } = [];
}

public sealed record UiPathRuleModuleImportResult
{
    public bool Success => Errors.Count == 0;
    public int ImportedRules { get; init; }
    public int SkippedRules { get; init; }
    public int ImportedProfiles { get; init; }
    public int SkippedProfiles { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}

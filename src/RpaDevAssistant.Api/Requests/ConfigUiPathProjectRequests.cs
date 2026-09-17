using RpaDevAssistant.Core.Config;

namespace RpaDevAssistant.Api.Requests;

public sealed record AnalyzeUiPathConfigRequest
{
    public string? ProjectPath { get; init; }

    public string? ConfigPath { get; init; }
}

public sealed record PreviewUiPathConfigChangesRequest
{
    public string? ProjectPath { get; init; }

    public string? ConfigPath { get; init; }

    public IReadOnlyList<string> RemoveKeys { get; init; } = [];

    public IReadOnlyList<UiPathConfigAdditionRequest> Additions { get; init; } = [];
}

public sealed record GenerateUiPathConfigRequest
{
    public string? ProjectPath { get; init; }

    public string? ConfigPath { get; init; }

    public string? OutputPath { get; init; }

    public IReadOnlyList<string> RemoveKeys { get; init; } = [];

    public IReadOnlyList<UiPathConfigAdditionRequest> Additions { get; init; } = [];
}

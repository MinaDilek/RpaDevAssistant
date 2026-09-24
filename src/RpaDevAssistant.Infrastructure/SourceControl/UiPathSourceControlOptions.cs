using RpaDevAssistant.Core.SourceControl;

namespace RpaDevAssistant.Infrastructure.SourceControl;

public sealed record UiPathSourceControlOptions
{
    public IReadOnlyDictionary<UiPathSourceControlProvider, UiPathSourceControlProviderOptions> Providers { get; init; } = new Dictionary<UiPathSourceControlProvider, UiPathSourceControlProviderOptions>();
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(15);
}

public sealed record UiPathSourceControlProviderOptions(Uri BaseUri, string? AccessToken)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccessToken);
}

namespace RpaDevAssistant.Core.Dependencies;

public sealed record NuGetPackageMetadataOptions
{
    public Uri NuGetServiceIndexUri { get; init; } = new("https://api.nuget.org/v3/index.json");

    public Uri UiPathServiceIndexUri { get; init; } = new("https://pkgs.uipath.com/official/index.json");

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(4);

    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromHours(6);

    public TimeSpan FailureCacheDuration { get; init; } = TimeSpan.FromMinutes(5);
}

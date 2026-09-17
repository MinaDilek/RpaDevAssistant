using RpaDevAssistant.Core.Dependencies;

namespace RpaDevAssistant.Api.Configuration;

public sealed record RpaDevAssistantRuntimeConfiguration
{
    public IReadOnlySet<string> AllowedOrigins { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public string? CustomRulesFilePath { get; init; }

    public string? RuleProfilesFilePath { get; init; }

    public string? AnalysisHistoryRoot { get; init; }

    public int MaxHistorySnapshotsPerProject { get; init; } = 20;

    public NuGetPackageMetadataOptions PackageMetadata { get; init; } = new();

    public static RpaDevAssistantRuntimeConfiguration Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var origins = configuration.GetSection("RpaDevAssistant:AllowedOrigins").Get<string[]>() ?? [];
        var legacyOrigins = Environment.GetEnvironmentVariable("RPADA_ALLOWED_ORIGINS")?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        return new RpaDevAssistantRuntimeConfiguration
        {
            AllowedOrigins = origins.Concat(legacyOrigins)
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Select(ValidateOrigin)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            CustomRulesFilePath = NullIfWhiteSpace(configuration["RpaDevAssistant:Storage:CustomRulesFile"]),
            RuleProfilesFilePath = NullIfWhiteSpace(configuration["RpaDevAssistant:Storage:RuleProfilesFile"]),
            AnalysisHistoryRoot = NullIfWhiteSpace(configuration["RpaDevAssistant:Storage:AnalysisHistoryRoot"]),
            MaxHistorySnapshotsPerProject = PositiveInt(configuration["RpaDevAssistant:History:MaxSnapshotsPerProject"], 20, "MaxSnapshotsPerProject"),
            PackageMetadata = new NuGetPackageMetadataOptions
            {
                NuGetServiceIndexUri = OfficialHttpsUri(configuration["RpaDevAssistant:PackageMetadata:NuGetServiceIndexUri"], "https://api.nuget.org/v3/index.json", "NuGetServiceIndexUri"),
                UiPathServiceIndexUri = OfficialHttpsUri(configuration["RpaDevAssistant:PackageMetadata:UiPathServiceIndexUri"], "https://pkgs.uipath.com/official/index.json", "UiPathServiceIndexUri"),
                RequestTimeout = PositiveDuration(configuration["RpaDevAssistant:PackageMetadata:RequestTimeoutSeconds"], 4, "RequestTimeoutSeconds"),
                CacheDuration = PositiveMinutes(configuration["RpaDevAssistant:PackageMetadata:CacheDurationMinutes"], 360, "CacheDurationMinutes"),
                FailureCacheDuration = PositiveMinutes(configuration["RpaDevAssistant:PackageMetadata:FailureCacheDurationMinutes"], 5, "FailureCacheDurationMinutes")
            }
        };
    }

    private static string ValidateOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || (uri.Scheme is not "http" and not "https" and not "tauri"))
        {
            throw new InvalidOperationException($"Configured desktop origin '{origin}' is invalid.");
        }

        return origin.TrimEnd('/');
    }

    private static Uri OfficialHttpsUri(string? configured, string fallback, string name)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? fallback : configured;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"{name} must be an absolute HTTPS URI.");
        }

        return uri;
    }

    private static int PositiveInt(string? value, int fallback, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (!int.TryParse(value, out var parsed) || parsed <= 0)
        {
            throw new InvalidOperationException($"{name} must be a positive integer.");
        }

        return parsed;
    }

    private static TimeSpan PositiveDuration(string? value, int fallback, string name)
        => TimeSpan.FromSeconds(PositiveInt(value, fallback, name));

    private static TimeSpan PositiveMinutes(string? value, int fallback, string name)
        => TimeSpan.FromMinutes(PositiveInt(value, fallback, name));

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

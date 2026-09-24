using RpaDevAssistant.Core.Dependencies;
using RpaDevAssistant.Infrastructure.Orchestrator;
using RpaDevAssistant.Core.SourceControl;
using RpaDevAssistant.Infrastructure.SourceControl;

namespace RpaDevAssistant.Api.Configuration;

public sealed record RpaDevAssistantRuntimeConfiguration
{
    public IReadOnlySet<string> AllowedOrigins { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public string? CustomRulesFilePath { get; init; }

    public string? RuleProfilesFilePath { get; init; }

    public string? AnalysisHistoryRoot { get; init; }

    public int MaxHistorySnapshotsPerProject { get; init; } = 20;

    public NuGetPackageMetadataOptions PackageMetadata { get; init; } = new();

    public RpaDevAssistantFeatureFlags FeatureFlags { get; init; } = new();

    public RpaDevAssistantDiagnosticsOptions Diagnostics { get; init; } = new();

    public UiPathOrchestratorOptions Orchestrator { get; init; } = new();

    public UiPathSourceControlOptions SourceControl { get; init; } = new();

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
            FeatureFlags = new RpaDevAssistantFeatureFlags
            {
                Ai = BooleanValue(configuration["RpaDevAssistant:Features:Ai"], true, "Features:Ai"),
                FileMutations = BooleanValue(configuration["RpaDevAssistant:Features:FileMutations"], true, "Features:FileMutations"),
                ConfigGeneration = BooleanValue(configuration["RpaDevAssistant:Features:ConfigGeneration"], true, "Features:ConfigGeneration"),
                FlowchartConversion = BooleanValue(configuration["RpaDevAssistant:Features:FlowchartConversion"], true, "Features:FlowchartConversion")
            },
            Diagnostics = new RpaDevAssistantDiagnosticsOptions
            {
                TelemetryEnabled = BooleanValue(configuration["RpaDevAssistant:Diagnostics:TelemetryEnabled"], false, "Diagnostics:TelemetryEnabled"),
                CrashReportingEnabled = BooleanValue(configuration["RpaDevAssistant:Diagnostics:CrashReportingEnabled"], true, "Diagnostics:CrashReportingEnabled"),
                StorageRoot = NullIfWhiteSpace(configuration["RpaDevAssistant:Storage:DiagnosticsRoot"])
            },
            Orchestrator = new UiPathOrchestratorOptions
            {
                BaseUri = OptionalHttpsUri(configuration["RpaDevAssistant:Orchestrator:BaseUrl"], "Orchestrator:BaseUrl"),
                AccessToken = NullIfWhiteSpace(configuration["RpaDevAssistant:Orchestrator:AccessToken"]),
                TenantName = NullIfWhiteSpace(configuration["RpaDevAssistant:Orchestrator:TenantName"]),
                FolderId = OptionalPositiveLong(configuration["RpaDevAssistant:Orchestrator:FolderId"], "Orchestrator:FolderId"),
                DeploymentType = NullIfWhiteSpace(configuration["RpaDevAssistant:Orchestrator:DeploymentType"]) ?? "AutomationCloud",
                RequestTimeout = PositiveDuration(configuration["RpaDevAssistant:Orchestrator:RequestTimeoutSeconds"], 15, "Orchestrator:RequestTimeoutSeconds")
            },
            SourceControl = LoadSourceControl(configuration),
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

    private static Uri? OptionalHttpsUri(string? configured, string name)
    {
        if (string.IsNullOrWhiteSpace(configured)) return null;
        if (!Uri.TryCreate(configured.TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"{name} must be an absolute HTTPS URI.");
        }
        return uri;
    }

    private static UiPathSourceControlOptions LoadSourceControl(IConfiguration configuration)
    {
        var providers = new Dictionary<UiPathSourceControlProvider, UiPathSourceControlProviderOptions>();
        AddProvider(UiPathSourceControlProvider.GitHub, "GitHub", "https://api.github.com/");
        AddProvider(UiPathSourceControlProvider.GitLab, "GitLab", "https://gitlab.com/api/v4/");
        AddProvider(UiPathSourceControlProvider.AzureDevOps, "AzureDevOps", null);
        return new UiPathSourceControlOptions
        {
            Providers = providers,
            RequestTimeout = PositiveDuration(configuration["RpaDevAssistant:SourceControl:RequestTimeoutSeconds"], 15, "SourceControl:RequestTimeoutSeconds")
        };

        void AddProvider(UiPathSourceControlProvider provider, string key, string? fallbackBaseUrl)
        {
            var token = NullIfWhiteSpace(configuration[$"RpaDevAssistant:SourceControl:{key}:AccessToken"]);
            var rawBaseUrl = NullIfWhiteSpace(configuration[$"RpaDevAssistant:SourceControl:{key}:BaseUrl"]) ?? fallbackBaseUrl;
            if (token is null && rawBaseUrl is null) return;
            var uri = OptionalHttpsUri(rawBaseUrl, $"SourceControl:{key}:BaseUrl");
            if (uri is null)
            {
                if (token is not null) throw new InvalidOperationException($"SourceControl:{key}:BaseUrl is required when an access token is configured.");
                return;
            }
            providers[provider] = new UiPathSourceControlProviderOptions(uri, token);
        }
    }

    private static long? OptionalPositiveLong(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!long.TryParse(value, out var parsed) || parsed <= 0) throw new InvalidOperationException($"{name} must be a positive integer.");
        return parsed;
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

    private static bool BooleanValue(string? value, bool fallback, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (bool.TryParse(value, out var parsed)) return parsed;
        throw new InvalidOperationException($"{name} must be true or false.");
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed record RpaDevAssistantFeatureFlags
{
    public bool Ai { get; init; } = true;
    public bool FileMutations { get; init; } = true;
    public bool ConfigGeneration { get; init; } = true;
    public bool FlowchartConversion { get; init; } = true;
}

public sealed record RpaDevAssistantDiagnosticsOptions
{
    public bool TelemetryEnabled { get; init; }
    public bool CrashReportingEnabled { get; init; } = true;
    public string? StorageRoot { get; init; }
}

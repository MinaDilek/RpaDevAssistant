using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace RpaDevAssistant.Core.Dependencies;

public sealed class NuGetUiPathPackageMetadataProvider : IUiPathPackageMetadataProvider
{
    private const string RegistrationResourceType = "RegistrationsBaseUrl";
    private readonly HttpClient httpClient;
    private readonly NuGetPackageMetadataOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<string, CacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<FeedKind, Uri> registrationBaseUris = new();

    public NuGetUiPathPackageMetadataProvider(
        HttpClient httpClient,
        NuGetPackageMetadataOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.options = options ?? new NuGetPackageMetadataOptions();
        this.timeProvider = timeProvider ?? TimeProvider.System;

        if (!IsOfficialEndpoint(this.options.NuGetServiceIndexUri, FeedKind.NuGet)
            || !IsOfficialEndpoint(this.options.UiPathServiceIndexUri, FeedKind.UiPath))
        {
            throw new ArgumentException("Only official NuGet metadata endpoints are supported.", nameof(options));
        }
    }

    public async Task<UiPathPackageMetadata> GetMetadataAsync(
        string packageName,
        string? declaredVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        var normalizedVersion = NormalizeDeclaredVersion(declaredVersion);
        var cacheKey = $"{packageName.Trim()}|{normalizedVersion}";
        var now = timeProvider.GetUtcNow();
        if (cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAtUtc > now)
        {
            return cached.Metadata;
        }

        var gate = locks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = timeProvider.GetUtcNow();
            if (cache.TryGetValue(cacheKey, out cached) && cached.ExpiresAtUtc > now)
            {
                return cached.Metadata;
            }

            var metadata = await FetchMetadataAsync(packageName.Trim(), normalizedVersion, cancellationToken).ConfigureAwait(false);
            var duration = metadata.Status == UiPathPackageMetadataStatus.Available
                ? options.CacheDuration
                : options.FailureCacheDuration;
            cache[cacheKey] = new CacheEntry(metadata, now.Add(duration));
            return metadata;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<UiPathPackageMetadata> FetchMetadataAsync(
        string packageName,
        string? declaredVersion,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.RequestTimeout);

        try
        {
            var feed = packageName.StartsWith("UiPath.", StringComparison.OrdinalIgnoreCase)
                ? FeedKind.UiPath
                : FeedKind.NuGet;
            var baseUri = registrationBaseUris.TryGetValue(feed, out var cachedBaseUri)
                ? cachedBaseUri
                : await DiscoverRegistrationBaseUriAsync(feed, timeout.Token).ConfigureAwait(false);
            registrationBaseUris.TryAdd(feed, baseUri);
            var packageUri = new Uri(baseUri, $"{Uri.EscapeDataString(packageName.ToLowerInvariant())}/index.json");
            using var index = await GetJsonAsync(packageUri, timeout.Token).ConfigureAwait(false);
            var entries = await ReadEntriesAsync(index.RootElement, feed, timeout.Token).ConfigureAwait(false);
            if (entries.Count == 0)
            {
                return UiPathPackageMetadata.Unknown;
            }

            var latestVersion = entries
                .Where(entry => entry.Listed && !entry.IsPrerelease)
                .OrderByDescending(entry => entry.Version, NuGetVersionComparer.Instance)
                .Select(entry => entry.VersionText)
                .FirstOrDefault();
            var declaredEntry = declaredVersion is null
                ? null
                : entries.FirstOrDefault(entry => entry.VersionText.Equals(declaredVersion, StringComparison.OrdinalIgnoreCase));

            return new UiPathPackageMetadata
            {
                Status = UiPathPackageMetadataStatus.Available,
                LatestVersion = latestVersion,
                DeprecationStatus = declaredEntry is null
                    ? UiPathPackageDeprecationStatus.Unknown
                    : declaredEntry.IsDeprecated
                        ? UiPathPackageDeprecationStatus.Deprecated
                        : UiPathPackageDeprecationStatus.NotDeprecated,
                DeprecationReasons = declaredEntry?.DeprecationReasons ?? [],
                AlternatePackage = declaredEntry?.AlternatePackage,
                VulnerabilityStatus = declaredEntry is null
                    ? UiPathPackageVulnerabilityStatus.Unknown
                    : declaredEntry.Vulnerabilities.Count > 0
                        ? UiPathPackageVulnerabilityStatus.Known
                        : UiPathPackageVulnerabilityStatus.None,
                Vulnerabilities = declaredEntry?.Vulnerabilities ?? [],
                RetrievedAtUtc = timeProvider.GetUtcNow()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UiPathPackageMetadata.Unknown;
        }
        catch (HttpRequestException)
        {
            return UiPathPackageMetadata.Unknown;
        }
        catch (JsonException)
        {
            return UiPathPackageMetadata.Unknown;
        }
        catch (InvalidOperationException)
        {
            return UiPathPackageMetadata.Unknown;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException and not StackOverflowException)
        {
            return UiPathPackageMetadata.Unknown;
        }
    }

    private async Task<Uri> DiscoverRegistrationBaseUriAsync(FeedKind feed, CancellationToken cancellationToken)
    {
        var serviceIndexUri = feed == FeedKind.UiPath
            ? options.UiPathServiceIndexUri
            : options.NuGetServiceIndexUri;
        using var document = await GetJsonAsync(serviceIndexUri, cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("resources", out var resources) || resources.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("NuGet service index does not contain resources.");
        }

        var candidates = resources.EnumerateArray()
            .Select(resource => new
            {
                Type = ReadString(resource, "@type"),
                Id = ReadString(resource, "@id")
            })
            .Where(resource => resource.Type?.StartsWith(RegistrationResourceType, StringComparison.OrdinalIgnoreCase) == true)
            .OrderByDescending(resource => ResourcePriority(resource.Type))
            .ToArray();

        foreach (var candidate in candidates)
        {
            if (Uri.TryCreate(candidate.Id, UriKind.Absolute, out var uri) && IsOfficialEndpoint(uri, feed))
            {
                return EnsureTrailingSlash(uri);
            }
        }

        throw new InvalidOperationException("Official NuGet registration metadata resource was not found.");
    }

    private async Task<List<RegistrationEntry>> ReadEntriesAsync(
        JsonElement root,
        FeedKind feed,
        CancellationToken cancellationToken)
    {
        var entries = new List<RegistrationEntry>();
        if (!root.TryGetProperty("items", out var pages) || pages.ValueKind != JsonValueKind.Array)
        {
            return entries;
        }

        foreach (var page in pages.EnumerateArray())
        {
            if (page.TryGetProperty("items", out var embeddedItems) && embeddedItems.ValueKind == JsonValueKind.Array)
            {
                ReadPageEntries(embeddedItems, entries);
                continue;
            }

            var pageUrl = ReadString(page, "@id");
            if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri) || !IsOfficialEndpoint(pageUri, feed))
            {
                continue;
            }

            using var pageDocument = await GetJsonAsync(pageUri, cancellationToken).ConfigureAwait(false);
            if (pageDocument.RootElement.TryGetProperty("items", out var pageItems) && pageItems.ValueKind == JsonValueKind.Array)
            {
                ReadPageEntries(pageItems, entries);
            }
        }

        return entries;
    }

    private static void ReadPageEntries(JsonElement items, ICollection<RegistrationEntry> entries)
    {
        foreach (var item in items.EnumerateArray())
        {
            var catalogEntry = item.TryGetProperty("catalogEntry", out var catalog) && catalog.ValueKind == JsonValueKind.Object
                ? catalog
                : item;
            var versionText = ReadString(catalogEntry, "version") ?? ReadString(item, "version");
            if (!NuGetVersion.TryParse(versionText, out var version))
            {
                continue;
            }

            var deprecation = catalogEntry.TryGetProperty("deprecation", out var deprecatedValue)
                && deprecatedValue.ValueKind == JsonValueKind.Object
                ? deprecatedValue
                : default;
            var reasons = deprecation.ValueKind == JsonValueKind.Object
                && deprecation.TryGetProperty("reasons", out var reasonValues)
                && reasonValues.ValueKind == JsonValueKind.Array
                    ? reasonValues.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()!).ToArray()
                    : [];
            var alternatePackage = deprecation.ValueKind == JsonValueKind.Object
                && deprecation.TryGetProperty("alternatePackage", out var alternate)
                && alternate.ValueKind == JsonValueKind.Object
                    ? ReadString(alternate, "id")
                    : null;
            var vulnerabilities = ReadVulnerabilities(catalogEntry);

            entries.Add(new RegistrationEntry(
                versionText!,
                version,
                !catalogEntry.TryGetProperty("listed", out var listed) || listed.ValueKind != JsonValueKind.False,
                versionText!.Contains('-', StringComparison.Ordinal),
                deprecation.ValueKind == JsonValueKind.Object,
                reasons,
                alternatePackage,
                vulnerabilities));
        }
    }

    private static IReadOnlyList<UiPathPackageVulnerability> ReadVulnerabilities(JsonElement catalogEntry)
    {
        if (!catalogEntry.TryGetProperty("vulnerabilities", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray()
            .Select(value => new UiPathPackageVulnerability
            {
                Severity = value.TryGetProperty("severity", out var severity) && severity.TryGetInt32(out var level) ? level : 0,
                AdvisoryUrl = ReadString(value, "advisoryUrl")
            })
            .ToArray();
    }

    private async Task<JsonDocument> GetJsonAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string? NormalizeDeclaredVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var normalized = version.Trim().Trim('[', ']', '(', ')');
        var separator = normalized.IndexOf(',');
        return separator >= 0 ? normalized[..separator].Trim() : normalized;
    }

    private static int ResourcePriority(string? type) => type switch
    {
        var value when value?.Contains("3.6.0", StringComparison.OrdinalIgnoreCase) == true => 3,
        var value when value?.Contains("3.4.0", StringComparison.OrdinalIgnoreCase) == true => 2,
        _ => 1
    };

    private static bool IsOfficialEndpoint(Uri uri, FeedKind feed)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (feed == FeedKind.NuGet)
        {
            return uri.Host.Equals("nuget.org", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".nuget.org", StringComparison.OrdinalIgnoreCase);
        }

        if (uri.Host.Equals("pkgs.uipath.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".uipath.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".pkgs.visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return uri.Host.Equals("pkgs.dev.azure.com", StringComparison.OrdinalIgnoreCase)
            && (uri.AbsolutePath.StartsWith("/uipath/", StringComparison.OrdinalIgnoreCase)
                || uri.AbsolutePath.StartsWith("/uipath-us/", StringComparison.OrdinalIgnoreCase));
    }

    private static Uri EnsureTrailingSlash(Uri uri) => uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
        ? uri
        : new Uri(uri.AbsoluteUri + "/");

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private sealed record CacheEntry(UiPathPackageMetadata Metadata, DateTimeOffset ExpiresAtUtc);

    private enum FeedKind
    {
        NuGet,
        UiPath
    }

    private sealed record RegistrationEntry(
        string VersionText,
        NuGetVersion Version,
        bool Listed,
        bool IsPrerelease,
        bool IsDeprecated,
        IReadOnlyList<string> DeprecationReasons,
        string? AlternatePackage,
        IReadOnlyList<UiPathPackageVulnerability> Vulnerabilities);

    private sealed record NuGetVersion(int Major, int Minor, int Patch, int Revision)
    {
        public static bool TryParse(string? value, out NuGetVersion version)
        {
            version = new NuGetVersion(0, 0, 0, 0);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var core = value.Split('-', 2)[0].Split('+', 2)[0];
            var parts = core.Split('.');
            if (parts.Length is < 1 or > 4)
            {
                return false;
            }

            var numbers = new int[4];
            for (var index = 0; index < parts.Length; index++)
            {
                if (!int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[index]))
                {
                    return false;
                }
            }

            version = new NuGetVersion(numbers[0], numbers[1], numbers[2], numbers[3]);
            return true;
        }
    }

    private sealed class NuGetVersionComparer : IComparer<NuGetVersion>
    {
        public static NuGetVersionComparer Instance { get; } = new();

        public int Compare(NuGetVersion? left, NuGetVersion? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            var result = left.Major.CompareTo(right.Major);
            if (result != 0) return result;
            result = left.Minor.CompareTo(right.Minor);
            if (result != 0) return result;
            result = left.Patch.CompareTo(right.Patch);
            return result != 0 ? result : left.Revision.CompareTo(right.Revision);
        }
    }
}

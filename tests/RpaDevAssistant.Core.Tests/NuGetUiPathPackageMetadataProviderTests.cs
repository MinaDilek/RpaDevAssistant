using System.Net;
using System.Text;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Rules;
using RpaDevAssistant.Core.Dependencies;
using RpaDevAssistant.Core.Models;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class NuGetUiPathPackageMetadataProviderTests
{
    [Fact]
    public async Task Provider_ResolvesLatestDeprecationAndVulnerabilityMetadata_AndCachesResult()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/official/index.json" => Json(ServiceIndex),
            "/official/registration/uipath.test.activities/index.json" => Json(RegistrationIndex),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var provider = Provider(handler);

        var first = await provider.GetMetadataAsync("UiPath.Test.Activities", "1.0.0");
        var second = await provider.GetMetadataAsync("UiPath.Test.Activities", "1.0.0");

        Assert.Equal(UiPathPackageMetadataStatus.Available, first.Status);
        Assert.Equal("2.0.0", first.LatestVersion);
        Assert.Equal(UiPathPackageDeprecationStatus.Deprecated, first.DeprecationStatus);
        Assert.Equal("UiPath.Replacement.Activities", first.AlternatePackage);
        Assert.Equal(UiPathPackageVulnerabilityStatus.Known, first.VulnerabilityStatus);
        Assert.Equal(3, Assert.Single(first.Vulnerabilities).Severity);
        Assert.Same(first, second);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Provider_LoadsNonEmbeddedRegistrationPage()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/official/index.json" => Json(ServiceIndex),
            "/official/registration/uipath.test.activities/index.json" => Json(PagedRegistrationIndex),
            "/official/registration/uipath.test.activities/page/1.0.0/2.0.0.json" => Json(RegistrationPage),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        var metadata = await Provider(handler).GetMetadataAsync("UiPath.Test.Activities", "2.0.0");

        Assert.Equal(UiPathPackageMetadataStatus.Available, metadata.Status);
        Assert.Equal("2.0.0", metadata.LatestVersion);
        Assert.Equal(UiPathPackageDeprecationStatus.NotDeprecated, metadata.DeprecationStatus);
        Assert.Equal(UiPathPackageVulnerabilityStatus.None, metadata.VulnerabilityStatus);
    }

    [Fact]
    public async Task Provider_UsesOfficialNuGetFeedForNonUiPathPackage()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/v3/index.json" => Json(NuGetServiceIndex),
            "/v3/registration5-gz-semver2/contoso.activities/index.json" => Json(RegistrationIndex),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        var metadata = await Provider(handler).GetMetadataAsync("Contoso.Activities", "1.0.0");

        Assert.Equal(UiPathPackageMetadataStatus.Available, metadata.Status);
        Assert.Equal("2.0.0", metadata.LatestVersion);
    }

    [Fact]
    public async Task Provider_ReturnsUnknownWhenNuGetIsUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("offline"));

        var metadata = await Provider(handler).GetMetadataAsync("UiPath.Test.Activities", "1.0.0");

        Assert.Equal(UiPathPackageMetadataStatus.Unknown, metadata.Status);
        Assert.Equal(UiPathPackageDeprecationStatus.Unknown, metadata.DeprecationStatus);
        Assert.Equal(UiPathPackageVulnerabilityStatus.Unknown, metadata.VulnerabilityStatus);
    }

    [Fact]
    public async Task Provider_ReturnsUnknownOnTimeout()
    {
        var handler = new AsyncStubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Json(ServiceIndex);
        });
        var provider = Provider(handler, TimeSpan.FromMilliseconds(20));

        var metadata = await provider.GetMetadataAsync("UiPath.Test.Activities", "1.0.0");

        Assert.Equal(UiPathPackageMetadataStatus.Unknown, metadata.Status);
    }

    [Fact]
    public async Task Provider_PropagatesCallerCancellation()
    {
        var provider = Provider(new StubHttpMessageHandler(_ => Json(ServiceIndex)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetMetadataAsync("UiPath.Test.Activities", "1.0.0", cancellation.Token));
    }

    [Fact]
    public async Task DependencyAnalyzer_EnrichesRiskAndRulesFromMetadata()
    {
        var metadata = new StubMetadataProvider(new UiPathPackageMetadata
        {
            Status = UiPathPackageMetadataStatus.Available,
            LatestVersion = "2.0.0",
            DeprecationStatus = UiPathPackageDeprecationStatus.Deprecated,
            DeprecationReasons = ["Legacy"],
            AlternatePackage = "UiPath.Replacement.Activities",
            VulnerabilityStatus = UiPathPackageVulnerabilityStatus.Known,
            Vulnerabilities = [new UiPathPackageVulnerability { Severity = 3, AdvisoryUrl = "https://github.com/advisories/GHSA-test" }],
            RetrievedAtUtc = DateTimeOffset.UtcNow
        });
        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/dependency-metadata",
            ProjectFolderExists = true,
            ProjectJsonExists = true,
            ProjectJsonParsed = true
        };
        project.Dependencies.Add(new UiPathDependency { Name = "UiPath.Test.Activities", Version = "[1.0.0]" });
        var analyzer = new UiPathDependencyAnalyzer(metadataProvider: metadata);

        project.DependencyAnalysis = await analyzer.AnalyzeAsync(project);
        var package = Assert.Single(project.DependencyAnalysis.Packages);

        Assert.Equal(UiPathDependencyVersionStatus.Outdated, package.VersionStatus);
        Assert.Equal(UiPathDependencyRiskLevel.Critical, package.RiskLevel);
        Assert.Equal(1, project.DependencyAnalysis.OutdatedDependencies);
        Assert.Equal(1, project.DependencyAnalysis.DeprecatedDependencies);
        Assert.Equal(1, project.DependencyAnalysis.VulnerableDependencies);
        Assert.Equal(0, project.DependencyAnalysis.UnknownMetadataDependencies);
        var context = new UiPathAnalysisContext { Project = project };
        Assert.Single(new PackageVersionAlignmentRiskRule().Analyze(context));
        Assert.Single(new LegacyPackageIndicatorRule().Analyze(context));
    }

    [Fact]
    public async Task DependencyAnalyzer_PreservesOfflineResultWhenMetadataIsUnknown()
    {
        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/dependency-offline",
            ProjectFolderExists = true,
            ProjectJsonExists = true,
            ProjectJsonParsed = true
        };
        project.Dependencies.Add(new UiPathDependency { Name = "UiPath.Test.Activities", Version = "[1.0.0]" });
        var analyzer = new UiPathDependencyAnalyzer(metadataProvider: new StubMetadataProvider(UiPathPackageMetadata.Unknown));

        var summary = await analyzer.AnalyzeAsync(project);
        var package = Assert.Single(summary.Packages);

        Assert.Equal(UiPathPackageMetadataStatus.Unknown, package.MetadataStatus);
        Assert.Equal(UiPathDependencyVersionStatus.Unknown, package.VersionStatus);
        Assert.Equal(1, summary.UnknownMetadataDependencies);
    }

    private static NuGetUiPathPackageMetadataProvider Provider(HttpMessageHandler handler, TimeSpan? timeout = null) =>
        new(
            new HttpClient(handler),
            new NuGetPackageMetadataOptions
            {
                RequestTimeout = timeout ?? TimeSpan.FromSeconds(1),
                CacheDuration = TimeSpan.FromMinutes(10),
                FailureCacheDuration = TimeSpan.FromMinutes(1)
            });

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private const string ServiceIndex = """
        {
          "resources": [
            {
              "@id": "https://pkgs.uipath.com/official/registration/",
              "@type": "RegistrationsBaseUrl/3.6.0"
            }
          ]
        }
        """;

    private const string NuGetServiceIndex = """
        {
          "resources": [
            {
              "@id": "https://api.nuget.org/v3/registration5-gz-semver2/",
              "@type": "RegistrationsBaseUrl/3.6.0"
            }
          ]
        }
        """;

    private const string RegistrationIndex = """
        {
          "items": [
            {
              "items": [
                {
                  "catalogEntry": {
                    "version": "1.0.0",
                    "listed": true,
                    "deprecation": {
                      "reasons": ["Legacy"],
                      "alternatePackage": { "id": "UiPath.Replacement.Activities", "range": "[2.0.0,)" }
                    },
                    "vulnerabilities": [
                      { "severity": 3, "advisoryUrl": "https://github.com/advisories/GHSA-test" }
                    ]
                  }
                },
                {
                  "catalogEntry": { "version": "2.0.0", "listed": true }
                },
                {
                  "catalogEntry": { "version": "3.0.0-beta.1", "listed": true }
                }
              ]
            }
          ]
        }
        """;

    private const string PagedRegistrationIndex = """
        {
          "items": [
            {
              "@id": "https://pkgs.uipath.com/official/registration/uipath.test.activities/page/1.0.0/2.0.0.json"
            }
          ]
        }
        """;

    private const string RegistrationPage = """
        {
          "items": [
            { "catalogEntry": { "version": "1.0.0", "listed": true } },
            { "catalogEntry": { "version": "2.0.0", "listed": true } }
          ]
        }
        """;

    private sealed class StubMetadataProvider(UiPathPackageMetadata metadata) : IUiPathPackageMetadataProvider
    {
        public Task<UiPathPackageMetadata> GetMetadataAsync(
            string packageName,
            string? declaredVersion,
            CancellationToken cancellationToken = default) => Task.FromResult(metadata);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(handler(request));
        }
    }

    private sealed class AsyncStubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}

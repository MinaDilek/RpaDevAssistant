using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class CentralAdministrationApiTests
{
    private const string BootstrapKey = "central-bootstrap-key-with-at-least-32-characters";

    [Fact]
    public async Task CentralMode_IsHiddenWhenDisabled()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/central/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CentralMode_EnforcesAuthenticationRolesAndTenantIsolation()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantCentralApi-{Guid.NewGuid():N}");
        Directory.CreateDirectory(storageRoot);
        try
        {
            using var factory = CreateFactory(storageRoot);
            using var anonymous = factory.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/central/status")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/central/catalog")).StatusCode);

            using var bootstrap = Client(factory, BootstrapKey);
            Assert.Equal(HttpStatusCode.OK, (await bootstrap.PostAsJsonAsync("/api/central/tenants", new
            {
                id = "tenant-a",
                name = "Tenant A"
            })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await bootstrap.PostAsJsonAsync("/api/central/tenants", new
            {
                id = "tenant-b",
                name = "Tenant B"
            })).StatusCode);

            var userResponse = await bootstrap.PostAsJsonAsync("/api/central/users", new
            {
                id = "admin-a",
                tenantId = "tenant-a",
                email = "admin-a@example.test",
                displayName = "Admin A",
                role = "TenantAdmin"
            });
            Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);
            using var userJson = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync());
            var apiKey = userJson.RootElement.GetProperty("apiKey").GetString();
            Assert.False(string.IsNullOrWhiteSpace(apiKey));

            using var tenantAdmin = Client(factory, apiKey!);
            var catalogResponse = await tenantAdmin.GetAsync("/api/central/catalog");
            Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);
            using var catalogJson = JsonDocument.Parse(await catalogResponse.Content.ReadAsStringAsync());
            Assert.Equal("tenant-a", catalogJson.RootElement.GetProperty("tenants")[0].GetProperty("id").GetString());
            Assert.Single(catalogJson.RootElement.GetProperty("tenants").EnumerateArray());

            var crossTenantResponse = await tenantAdmin.PostAsJsonAsync("/api/central/teams", new
            {
                id = "team-b",
                tenantId = "tenant-b",
                name = "Forbidden Team"
            });
            Assert.Equal(HttpStatusCode.Forbidden, crossTenantResponse.StatusCode);

            var auditResponse = await tenantAdmin.GetAsync("/api/central/audit");
            Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
            using var auditJson = JsonDocument.Parse(await auditResponse.Content.ReadAsStringAsync());
            Assert.Contains(auditJson.RootElement.GetProperty("events").EnumerateArray(), item =>
                item.GetProperty("action").GetString() == "User.Save");
        }
        finally
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CentralAnalysis_UsesRegisteredProjectEnforcesQuotaAndBuildsDashboard()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantCentralApi-{Guid.NewGuid():N}");
        Directory.CreateDirectory(storageRoot);
        try
        {
            using var factory = CreateFactory(storageRoot);
            using var bootstrap = Client(factory, BootstrapKey);
            Assert.Equal(HttpStatusCode.OK, (await bootstrap.PostAsJsonAsync("/api/central/tenants", new
            {
                id = "tenant-a",
                name = "Tenant A",
                monthlyAnalysisQuota = 1
            })).StatusCode);
            var userResponse = await bootstrap.PostAsJsonAsync("/api/central/users", new
            {
                id = "developer-a",
                tenantId = "tenant-a",
                email = "developer-a@example.test",
                displayName = "Developer A",
                role = "Developer"
            });
            using var userJson = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync());
            var apiKey = userJson.RootElement.GetProperty("apiKey").GetString()!;
            Assert.Equal(HttpStatusCode.OK, (await bootstrap.PostAsJsonAsync("/api/central/projects", new
            {
                id = "project-a",
                tenantId = "tenant-a",
                name = "Validation Project",
                projectPath = FixturePath(),
                ruleProfileId = "default"
            })).StatusCode);

            using var developer = Client(factory, apiKey);
            var analysisResponse = await developer.PostAsync("/api/central/projects/project-a/analyze", null);
            Assert.Equal(HttpStatusCode.OK, analysisResponse.StatusCode);
            var quotaResponse = await developer.PostAsync("/api/central/projects/project-a/analyze", null);
            Assert.Equal(HttpStatusCode.BadRequest, quotaResponse.StatusCode);
            Assert.Contains("quota", await quotaResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            var historyResponse = await developer.GetAsync("/api/central/history?projectId=project-a");
            Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
            using var historyJson = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync());
            var analysis = Assert.Single(historyJson.RootElement.GetProperty("analyses").EnumerateArray());
            Assert.Equal("Completed", analysis.GetProperty("status").GetString());
            Assert.True(analysis.GetProperty("score").GetInt32() < 100);

            var dashboardResponse = await developer.GetAsync("/api/central/dashboard");
            Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
            using var dashboardJson = JsonDocument.Parse(await dashboardResponse.Content.ReadAsStringAsync());
            Assert.Equal(1, dashboardJson.RootElement.GetProperty("projectCount").GetInt32());
            Assert.Equal(1, dashboardJson.RootElement.GetProperty("monthlyUsage").GetInt32());
            Assert.Equal(1, dashboardJson.RootElement.GetProperty("monthlyQuota").GetInt32());
        }
        finally
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CentralAnalysis_UsesTenantSpecificRuleProfileWithoutLeakingIt()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantCentralApi-{Guid.NewGuid():N}");
        Directory.CreateDirectory(storageRoot);
        try
        {
            using var factory = CreateFactory(storageRoot);
            using var bootstrap = Client(factory, BootstrapKey);
            await bootstrap.PostAsJsonAsync("/api/central/tenants", new { id = "tenant-a", name = "Tenant A" });
            await bootstrap.PostAsJsonAsync("/api/central/tenants", new { id = "tenant-b", name = "Tenant B" });
            var profileResponse = await bootstrap.PostAsJsonAsync("/api/central/rule-profiles?tenantId=tenant-a", new
            {
                id = "default",
                name = "Tenant A Default",
                description = "Tenant-owned profile",
                rules = Array.Empty<object>()
            });
            Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
            await bootstrap.PostAsJsonAsync("/api/central/projects", new
            {
                id = "project-a",
                tenantId = "tenant-a",
                name = "Validation Project",
                projectPath = FixturePath(),
                ruleProfileId = "default"
            });
            var userResponse = await bootstrap.PostAsJsonAsync("/api/central/users", new
            {
                id = "developer-a",
                tenantId = "tenant-a",
                email = "profile-user@example.test",
                displayName = "Developer A",
                role = "Developer"
            });
            using var userJson = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync());
            using var developer = Client(factory, userJson.RootElement.GetProperty("apiKey").GetString()!);

            var analysisResponse = await developer.PostAsync("/api/central/projects/project-a/analyze", null);
            Assert.Equal(HttpStatusCode.OK, analysisResponse.StatusCode);
            using var analysisJson = JsonDocument.Parse(await analysisResponse.Content.ReadAsStringAsync());
            var result = analysisJson.RootElement.GetProperty("result");
            Assert.Equal(100, result.GetProperty("qualityScore").GetProperty("score").GetInt32());
            Assert.Equal(0, result.GetProperty("analysis").GetProperty("totalFindings").GetInt32());

            var tenantBProfiles = await bootstrap.GetAsync("/api/central/rule-profiles?tenantId=tenant-b");
            using var tenantBJson = JsonDocument.Parse(await tenantBProfiles.Content.ReadAsStringAsync());
            Assert.DoesNotContain(tenantBJson.RootElement.GetProperty("profiles").EnumerateArray(), item =>
                item.GetProperty("profile").GetProperty("name").GetString() == "Tenant A Default");
        }
        finally
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CentralReport_AppliesTenantBrandingAndRegisteredProjectProfile()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantCentralApi-{Guid.NewGuid():N}");
        Directory.CreateDirectory(storageRoot);
        try
        {
            using var factory = CreateFactory(storageRoot);
            using var bootstrap = Client(factory, BootstrapKey);
            await bootstrap.PostAsJsonAsync("/api/central/tenants", new
            {
                id = "tenant-a", name = "Tenant A", brandingName = "Acme Automation", brandingAccentColor = "#123456"
            });
            await bootstrap.PostAsJsonAsync("/api/central/projects", new
            {
                id = "project-a", tenantId = "tenant-a", name = "Validation Project", projectPath = FixturePath(), ruleProfileId = "default"
            });

            var response = await bootstrap.PostAsJsonAsync("/api/central/projects/project-a/report", new { format = "html", locale = "en" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Acme Automation", html, StringComparison.Ordinal);
            Assert.Contains("#123456", html, StringComparison.OrdinalIgnoreCase);

            var historyResponse = await bootstrap.GetAsync("/api/central/history?tenantId=tenant-a&projectId=project-a");
            Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
            using var historyJson = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync());
            var analysis = Assert.Single(historyJson.RootElement.GetProperty("analyses").EnumerateArray());
            Assert.Equal("Completed", analysis.GetProperty("status").GetString());
            Assert.Equal("default", analysis.GetProperty("profileId").GetString());
        }
        finally
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string storageRoot) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RpaDevAssistant:Central:Enabled", "true");
            builder.UseSetting("RpaDevAssistant:Central:StorageRoot", storageRoot);
            builder.UseSetting("RpaDevAssistant:Central:BootstrapApiKey", BootstrapKey);
        });

    private static HttpClient Client(WebApplicationFactory<Program> factory, string apiKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return client;
    }

    private static string FixturePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RpaDevAssistant.sln")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException(), "samples", "RealisticValidationProject");
    }
}

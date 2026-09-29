using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class ScimProvisioningApiTests
{
    private const string BootstrapKey = "scim-bootstrap-key-with-at-least-32-characters";
    private const string UserSchema = "urn:ietf:params:scim:schemas:core:2.0:User";
    private const string PatchSchema = "urn:ietf:params:scim:api:messages:2.0:PatchOp";

    [Fact]
    public async Task Scim_ProvisionsFiltersDeactivatesAndIsolatesTenantUsers()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"rpada-scim-{Guid.NewGuid():N}");
        try
        {
            using var factory = CreateFactory(storageRoot);
            using var bootstrap = Client(factory, BootstrapKey);
            var tenantAKey = await ProvisionTenantAdministrator(bootstrap, "tenant-a", "admin-a");
            var tenantBKey = await ProvisionTenantAdministrator(bootstrap, "tenant-b", "admin-b");
            using var tenantA = Client(factory, tenantAKey);
            using var tenantB = Client(factory, tenantBKey);

            var replaceAdministrator = await tenantA.PutAsJsonAsync("/api/scim/v2/Users/admin-a", new
            {
                schemas = new[] { UserSchema }, userName = "admin-a@example.test", displayName = "Updated Administrator",
                active = true, externalId = "entra-admin-a"
            });
            Assert.Equal(HttpStatusCode.OK, replaceAdministrator.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await tenantA.GetAsync("/api/central/catalog")).StatusCode);

            var create = await tenantA.PostAsJsonAsync("/api/scim/v2/Users", new
            {
                schemas = new[] { UserSchema }, userName = "developer@example.test", displayName = "SCIM Developer",
                active = true, externalId = "entra-subject-123"
            });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
            var userId = created.RootElement.GetProperty("id").GetString();
            Assert.False(string.IsNullOrWhiteSpace(userId));
            Assert.Equal("entra-subject-123", created.RootElement.GetProperty("externalId").GetString());

            var filtered = await tenantA.GetAsync("/api/scim/v2/Users?filter=userName%20eq%20%22developer%40example.test%22");
            Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
            using var filteredJson = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
            Assert.Equal(1, filteredJson.RootElement.GetProperty("totalResults").GetInt32());

            var isolated = await tenantB.GetAsync("/api/scim/v2/Users");
            Assert.Equal(HttpStatusCode.OK, isolated.StatusCode);
            using var isolatedJson = JsonDocument.Parse(await isolated.Content.ReadAsStringAsync());
            Assert.Equal(1, isolatedJson.RootElement.GetProperty("totalResults").GetInt32());
            Assert.DoesNotContain("developer@example.test", await isolated.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            var patch = new HttpRequestMessage(HttpMethod.Patch, $"/api/scim/v2/Users/{userId}")
            {
                Content = JsonContent.Create(new
                {
                    schemas = new[] { PatchSchema },
                    operations = new[] { new { op = "replace", path = "active", value = false } }
                })
            };
            var patched = await tenantA.SendAsync(patch);
            Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
            using var patchedJson = JsonDocument.Parse(await patched.Content.ReadAsStringAsync());
            Assert.False(patchedJson.RootElement.GetProperty("active").GetBoolean());

            var catalog = await tenantA.GetAsync("/api/central/catalog");
            Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
            using var catalogJson = JsonDocument.Parse(await catalog.Content.ReadAsStringAsync());
            var provisioned = catalogJson.RootElement.GetProperty("users").EnumerateArray().Single(item => item.GetProperty("id").GetString() == userId);
            Assert.Equal("Viewer", provisioned.GetProperty("role").GetString());
            Assert.False(provisioned.GetProperty("active").GetBoolean());

            var badFilter = await tenantA.GetAsync("/api/scim/v2/Users?filter=displayName%20co%20%22SCIM%22");
            Assert.Equal(HttpStatusCode.BadRequest, badFilter.StatusCode);
            using var badFilterJson = JsonDocument.Parse(await badFilter.Content.ReadAsStringAsync());
            Assert.Equal("invalidFilter", badFilterJson.RootElement.GetProperty("scimType").GetString());
        }
        finally
        {
            if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Scim_RequiresTenantAdministratorCredential()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"rpada-scim-{Guid.NewGuid():N}");
        try
        {
            using var factory = CreateFactory(storageRoot);
            using var anonymous = factory.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/scim/v2/Users")).StatusCode);

            using var bootstrap = Client(factory, BootstrapKey);
            await bootstrap.PostAsJsonAsync("/api/central/tenants", new { id = "tenant-a", name = "Tenant A" });
            var response = await bootstrap.PostAsJsonAsync("/api/central/users", new
            {
                id = "viewer", tenantId = "tenant-a", email = "viewer@example.test", displayName = "Viewer", role = "Viewer"
            });
            response.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            using var viewer = Client(factory, body.RootElement.GetProperty("apiKey").GetString()!);
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/scim/v2/Users")).StatusCode);
        }
        finally
        {
            if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Scim_IsUnavailableWhenCentralModeIsDisabled()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), $"rpada-scim-disabled-{Guid.NewGuid():N}");
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("RpaDevAssistant:Central:Enabled", "false");
                builder.UseSetting("RpaDevAssistant:Central:StorageRoot", storageRoot);
            });
            using var client = factory.CreateClient();

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/scim/v2/ServiceProviderConfig")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/scim/v2/Users")).StatusCode);
        }
        finally
        {
            if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
        }
    }

    private static async Task<string> ProvisionTenantAdministrator(HttpClient bootstrap, string tenantId, string userId)
    {
        var tenant = await bootstrap.PostAsJsonAsync("/api/central/tenants", new { id = tenantId, name = tenantId });
        tenant.EnsureSuccessStatusCode();
        var user = await bootstrap.PostAsJsonAsync("/api/central/users", new
        {
            id = userId, tenantId, email = $"{userId}@example.test", displayName = userId, role = "TenantAdmin"
        });
        user.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await user.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("apiKey").GetString()!;
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
}

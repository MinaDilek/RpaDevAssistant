using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class FeatureFlagIntegrationTests
{
    [Fact]
    public async Task FeaturesEndpoint_ExposesEffectiveFlags()
    {
        using var factory = Factory(new Dictionary<string, string?>
        {
            ["RpaDevAssistant:Features:Ai"] = "false"
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/features");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("ai").GetBoolean());
        Assert.True(json.RootElement.GetProperty("fileMutations").GetBoolean());
    }

    [Fact]
    public async Task DisabledFeature_BlocksOnlyItsMappedEndpoints()
    {
        using var factory = Factory(new Dictionary<string, string?>
        {
            ["RpaDevAssistant:Features:Ai"] = "false"
        });
        using var client = factory.CreateClient();

        var aiResponse = await client.PostAsync("/api/uipath/projects/ai-review", JsonContent.Create(new { }));
        var healthResponse = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.NotFound, aiResponse.StatusCode);
        Assert.Contains("FEATURE_DISABLED", await aiResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
    }

    private static WebApplicationFactory<Program> Factory(Dictionary<string, string?> values)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in values)
            {
                builder.UseSetting(key, value);
            }
        });
    }
}

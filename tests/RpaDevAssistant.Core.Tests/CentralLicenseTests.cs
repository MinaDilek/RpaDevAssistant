using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RpaDevAssistant.Core.Central;
using RpaDevAssistant.Infrastructure.Central;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class CentralLicenseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void License_ValidatesSignatureExpiryAndEntitlements()
    {
        using var directory = new TemporaryDirectory();
        using var rsa = RSA.Create(2048);
        var payload = new CentralLicensePayload
        {
            LicenseId = "license-1",
            CustomerId = "customer-1",
            Plan = CentralSubscriptionPlan.Team,
            IssuedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            MaxTenants = 1,
            MaxMonthlyAnalyses = 100
        };
        var service = CreateService(directory.Path, rsa, payload);

        Assert.Equal(CentralLicenseState.Valid, service.GetStatus().State);
        service.ValidateTenantChange([], Tenant("tenant-a", CentralSubscriptionPlan.Team, 100));
        Assert.Throws<CentralLicenseException>(() => service.ValidateTenantChange([Tenant("tenant-a", CentralSubscriptionPlan.Team, 100)], Tenant("tenant-b", CentralSubscriptionPlan.Team, 10)));
        Assert.Throws<CentralLicenseException>(() => service.ValidateTenantChange([], Tenant("tenant-a", CentralSubscriptionPlan.Enterprise, 10)));
        Assert.Throws<CentralLicenseException>(() => service.ValidateTenantChange([], Tenant("tenant-a", CentralSubscriptionPlan.Team, 101)));
    }

    [Fact]
    public void License_FailsClosedAfterPayloadTampering()
    {
        using var directory = new TemporaryDirectory();
        using var rsa = RSA.Create(2048);
        var payload = new CentralLicensePayload
        {
            LicenseId = "license-1", CustomerId = "customer-1", Plan = CentralSubscriptionPlan.Team,
            IssuedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1), ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30)
        };
        var service = CreateService(directory.Path, rsa, payload);
        var licensePath = Path.Combine(directory.Path, "license.json");
        File.WriteAllText(licensePath, File.ReadAllText(licensePath).Replace("customer-1", "customer-2", StringComparison.Ordinal));

        Assert.Equal(CentralLicenseState.Invalid, service.GetStatus().State);
    }

    [Fact]
    public void License_ReportsExpiredAndSupportsExplicitInternalNoLicenseMode()
    {
        using var directory = new TemporaryDirectory();
        using var rsa = RSA.Create(2048);
        var payload = new CentralLicensePayload
        {
            LicenseId = "expired", CustomerId = "customer-1", Plan = CentralSubscriptionPlan.Team,
            IssuedAtUtc = DateTimeOffset.UtcNow.AddDays(-2), ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };
        Assert.Equal(CentralLicenseState.Expired, CreateService(directory.Path, rsa, payload).GetStatus().State);
        Assert.Equal(CentralLicenseState.NotRequired, new SignedCentralLicenseService(new CentralLicenseOptions { Required = false }).GetStatus().State);
    }

    private static SignedCentralLicenseService CreateService(string directory, RSA rsa, CentralLicensePayload payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(payloadJson), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var licensePath = Path.Combine(directory, "license.json");
        var publicKeyPath = Path.Combine(directory, "public.pem");
        File.WriteAllText(licensePath, $"{{\"payload\":{payloadJson},\"signature\":\"{Convert.ToBase64String(signature)}\"}}");
        File.WriteAllText(publicKeyPath, rsa.ExportSubjectPublicKeyInfoPem());
        return new SignedCentralLicenseService(new CentralLicenseOptions { Required = true, LicenseFile = licensePath, PublicKeyFile = publicKeyPath });
    }

    private static CentralTenant Tenant(string id, CentralSubscriptionPlan plan, int quota) => new()
    {
        Id = id, Name = id, Plan = plan, MonthlyAnalysisQuota = quota, CreatedAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"RpaLicense-{Guid.NewGuid():N}"); Directory.CreateDirectory(Path); }
        public string Path { get; }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

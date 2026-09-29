using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RpaDevAssistant.Core.Central;

namespace RpaDevAssistant.Infrastructure.Central;

public sealed record CentralLicenseOptions
{
    public bool Required { get; init; }
    public string? LicenseFile { get; init; }
    public string? PublicKeyFile { get; init; }
}

public sealed class SignedCentralLicenseService(CentralLicenseOptions options) : ICentralLicenseService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public CentralLicenseStatus GetStatus()
    {
        if (!options.Required) return new(CentralLicenseState.NotRequired, null, null, null, null, null);
        if (string.IsNullOrWhiteSpace(options.LicenseFile) || string.IsNullOrWhiteSpace(options.PublicKeyFile)
            || !File.Exists(options.LicenseFile) || !File.Exists(options.PublicKeyFile))
            return new(CentralLicenseState.Missing, null, null, null, null, "A signed license and public key are required.");

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(options.LicenseFile));
            var root = document.RootElement;
            var payloadElement = root.GetProperty("payload");
            var signature = Convert.FromBase64String(root.GetProperty("signature").GetString() ?? string.Empty);
            var payloadBytes = Encoding.UTF8.GetBytes(payloadElement.GetRawText());
            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(options.PublicKeyFile));
            if (!rsa.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                return new(CentralLicenseState.Invalid, null, null, null, null, "The license signature is invalid.");
            var payload = payloadElement.Deserialize<CentralLicensePayload>(JsonOptions)
                ?? throw new JsonException("The license payload is empty.");
            ValidatePayload(payload);
            if (payload.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                return new(CentralLicenseState.Expired, payload.LicenseId, payload.CustomerId, payload.Plan, payload.ExpiresAtUtc, "The license has expired.");
            return new(CentralLicenseState.Valid, payload.LicenseId, payload.CustomerId, payload.Plan, payload.ExpiresAtUtc, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or CryptographicException or ArgumentException)
        {
            return new(CentralLicenseState.Invalid, null, null, null, null, "The license could not be validated.");
        }
    }

    public void ValidateTenantChange(IReadOnlyList<CentralTenant> existingTenants, CentralTenant tenant)
    {
        var status = GetStatus();
        if (!status.AllowsAccess) throw new CentralLicenseException(status.State, status.Error ?? "The central license is not valid.");
        if (status.State == CentralLicenseState.NotRequired) return;
        var payload = ReadVerifiedPayload();
        var activeTenantIds = existingTenants.Where(item => item.Active && item.Id != tenant.Id).Select(item => item.Id);
        if (tenant.Active) activeTenantIds = activeTenantIds.Append(tenant.Id);
        var activeTenantCount = activeTenantIds.Distinct(StringComparer.Ordinal).Count();
        if (activeTenantCount > payload.MaxTenants)
            throw new CentralLicenseException(CentralLicenseState.Valid, $"The license allows at most {payload.MaxTenants} active tenant(s).");
        if (tenant.Plan > payload.Plan)
            throw new CentralLicenseException(CentralLicenseState.Valid, $"The {tenant.Plan} plan is not included in this license.");
        if (tenant.MonthlyAnalysisQuota > payload.MaxMonthlyAnalyses)
            throw new CentralLicenseException(CentralLicenseState.Valid, $"The tenant quota exceeds the licensed monthly analysis limit of {payload.MaxMonthlyAnalyses}.");
    }

    private CentralLicensePayload ReadVerifiedPayload()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(options.LicenseFile!));
        return document.RootElement.GetProperty("payload").Deserialize<CentralLicensePayload>(JsonOptions)
            ?? throw new CentralLicenseException(CentralLicenseState.Invalid, "The license payload is empty.");
    }

    private static void ValidatePayload(CentralLicensePayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.LicenseId) || string.IsNullOrWhiteSpace(payload.CustomerId)
            || payload.MaxTenants <= 0 || payload.MaxMonthlyAnalyses <= 0 || payload.ExpiresAtUtc <= payload.IssuedAtUtc)
            throw new JsonException("The license payload contains invalid limits or identifiers.");
    }
}

public sealed class CentralLicenseException(CentralLicenseState state, string message) : InvalidOperationException(message)
{
    public CentralLicenseState State { get; } = state;
}

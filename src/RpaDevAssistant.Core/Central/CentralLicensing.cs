namespace RpaDevAssistant.Core.Central;

public enum CentralLicenseState
{
    NotRequired,
    Valid,
    Missing,
    Invalid,
    Expired
}

public sealed record CentralLicensePayload
{
    public required string LicenseId { get; init; }
    public required string CustomerId { get; init; }
    public required CentralSubscriptionPlan Plan { get; init; }
    public required DateTimeOffset IssuedAtUtc { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public int MaxTenants { get; init; } = 1;
    public int MaxMonthlyAnalyses { get; init; } = 1_000;
}

public sealed record CentralLicenseStatus(CentralLicenseState State, string? LicenseId, string? CustomerId, CentralSubscriptionPlan? Plan, DateTimeOffset? ExpiresAtUtc, string? Error)
{
    public bool AllowsAccess => State is CentralLicenseState.NotRequired or CentralLicenseState.Valid;
}

public interface ICentralLicenseService
{
    CentralLicenseStatus GetStatus();

    void ValidateTenantChange(IReadOnlyList<CentralTenant> existingTenants, CentralTenant tenant);
}

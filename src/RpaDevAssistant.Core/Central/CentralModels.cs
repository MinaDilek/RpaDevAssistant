using RpaDevAssistant.Core.Analysis.Profiles;

namespace RpaDevAssistant.Core.Central;

public enum CentralRole
{
    Viewer,
    Developer,
    Manager,
    TenantAdmin,
    SystemAdmin
}

public enum CentralSubscriptionPlan
{
    Internal,
    Trial,
    Team,
    Enterprise
}

public sealed record CentralTenant
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool Active { get; init; } = true;
    public CentralSubscriptionPlan Plan { get; init; } = CentralSubscriptionPlan.Internal;
    public int MonthlyAnalysisQuota { get; init; } = 1_000;
    public string? BrandingName { get; init; }
    public string? BrandingAccentColor { get; init; }
    public string? ExternalIdentityId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record CentralUser
{
    public required string Id { get; init; }
    public required string TenantId { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public CentralRole Role { get; init; }
    public bool Active { get; init; } = true;
    public IReadOnlyList<string> TeamIds { get; init; } = [];
    public string? ExternalSubject { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record CentralTeam
{
    public required string Id { get; init; }
    public required string TenantId { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<string> MemberUserIds { get; init; } = [];
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record CentralProject
{
    public required string Id { get; init; }
    public required string TenantId { get; init; }
    public required string Name { get; init; }
    public required string ProjectPath { get; init; }
    public string RuleProfileId { get; init; } = "default";
    public string? TeamId { get; init; }
    public bool Active { get; init; } = true;
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record CentralAuditEvent
{
    public required string OperationId { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required string TenantId { get; init; }
    public required string ActorUserId { get; init; }
    public required string Action { get; init; }
    public required string ResourceType { get; init; }
    public required string ResourceId { get; init; }
    public string Outcome { get; init; } = "Success";
}

public sealed record CentralAuditIntegrity(bool Valid, int EventCount, int LegacyEventCount, string? LastHash, string? Error);

public enum CentralAnalysisStatus
{
    Running,
    Completed,
    Failed
}

public sealed record CentralAnalysisRecord
{
    public required string Id { get; init; }
    public required string TenantId { get; init; }
    public required string ProjectId { get; init; }
    public required string UserId { get; init; }
    public string? TeamId { get; init; }
    public required string ProfileId { get; init; }
    public CentralAnalysisStatus Status { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public int? Score { get; init; }
    public string? Grade { get; init; }
    public int? WorkflowCount { get; init; }
    public int? ActivityCount { get; init; }
    public int? FindingCount { get; init; }
    public int? CriticalCount { get; init; }
    public int? ErrorCount { get; init; }
    public int? WarningCount { get; init; }
    public int? SuggestionCount { get; init; }
    public string? FailureCode { get; init; }
}

public sealed record CentralTenantRuleProfile
{
    public required string TenantId { get; init; }
    public required UiPathRuleProfile Profile { get; init; }
    public required string UpdatedByUserId { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record CentralPrincipal(
    string UserId,
    string TenantId,
    CentralRole Role,
    string DisplayName,
    bool IsBootstrapAdministrator = false);

public sealed record CentralUserCredential(CentralUser User, string ApiKey);

public sealed record CentralCatalogSnapshot
{
    public IReadOnlyList<CentralTenant> Tenants { get; init; } = [];
    public IReadOnlyList<CentralUser> Users { get; init; } = [];
    public IReadOnlyList<CentralTeam> Teams { get; init; } = [];
    public IReadOnlyList<CentralProject> Projects { get; init; } = [];
    public IReadOnlyList<CentralAnalysisRecord> Analyses { get; init; } = [];
    public IReadOnlyList<CentralTenantRuleProfile> RuleProfiles { get; init; } = [];
}

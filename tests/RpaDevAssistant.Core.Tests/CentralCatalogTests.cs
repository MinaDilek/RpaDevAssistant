using RpaDevAssistant.Core.Central;
using RpaDevAssistant.Infrastructure.Central;
using RpaDevAssistant.Core.Analysis.Profiles;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class CentralCatalogTests
{
    [Fact]
    public void Catalog_PersistsTenantsUsersTeamsProjectsAndAuthenticatesHashedKeys()
    {
        using var directory = new TemporaryDirectory();
        var repository = new FileCentralCatalogRepository(new CentralCatalogOptions { StorageRoot = directory.Path });
        repository.SaveTenant(Tenant("tenant-a"));
        var credential = repository.SaveUser(User("admin-a", "tenant-a", CentralRole.TenantAdmin));
        repository.SaveTeam(new CentralTeam
        {
            Id = "team-a",
            TenantId = "tenant-a",
            Name = "Automation",
            MemberUserIds = ["admin-a"],
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        repository.SaveProject(new CentralProject
        {
            Id = "project-a",
            TenantId = "tenant-a",
            Name = "Invoices",
            ProjectPath = directory.Path,
            TeamId = "team-a",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        var reloaded = new FileCentralCatalogRepository(new CentralCatalogOptions { StorageRoot = directory.Path });
        var principal = reloaded.Authenticate(credential.ApiKey);

        Assert.NotNull(principal);
        Assert.Equal("tenant-a", principal.TenantId);
        Assert.Single(reloaded.GetSnapshot().Projects);
        var persistedJson = File.ReadAllText(System.IO.Path.Combine(directory.Path, "catalog.json"));
        Assert.DoesNotContain(credential.ApiKey, persistedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_RejectsCrossTenantTeamMembership()
    {
        using var directory = new TemporaryDirectory();
        var repository = new FileCentralCatalogRepository(new CentralCatalogOptions { StorageRoot = directory.Path });
        repository.SaveTenant(Tenant("tenant-a"));
        repository.SaveTenant(Tenant("tenant-b"));
        repository.SaveUser(User("user-b", "tenant-b", CentralRole.Developer));

        var error = Assert.Throws<InvalidOperationException>(() => repository.SaveTeam(new CentralTeam
        {
            Id = "team-a",
            TenantId = "tenant-a",
            Name = "Automation",
            MemberUserIds = ["user-b"],
            CreatedAtUtc = DateTimeOffset.UtcNow
        }));

        Assert.Contains("same tenant", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_AuditIsTenantScopedAndNewestFirst()
    {
        using var directory = new TemporaryDirectory();
        var repository = new FileCentralCatalogRepository(new CentralCatalogOptions { StorageRoot = directory.Path });
        repository.AppendAudit(Audit("tenant-a", "old", DateTimeOffset.UtcNow.AddMinutes(-1)));
        repository.AppendAudit(Audit("tenant-b", "other", DateTimeOffset.UtcNow));
        repository.AppendAudit(Audit("tenant-a", "new", DateTimeOffset.UtcNow));

        var events = repository.GetAuditEvents("tenant-a");

        Assert.Equal(2, events.Count);
        Assert.Equal("new", events[0].ResourceId);
        Assert.Equal("old", events[1].ResourceId);
        var integrity = repository.VerifyAuditIntegrity();
        Assert.True(integrity.Valid);
        Assert.Equal(3, integrity.EventCount);
        Assert.NotNull(integrity.LastHash);
    }

    [Fact]
    public void Catalog_AuditChainDetectsTamperingAndBlocksFurtherWrites()
    {
        using var directory = new TemporaryDirectory();
        var repository = new FileCentralCatalogRepository(new CentralCatalogOptions { StorageRoot = directory.Path });
        repository.AppendAudit(Audit("tenant-a", "original", DateTimeOffset.UtcNow));
        repository.AppendAudit(Audit("tenant-a", "second", DateTimeOffset.UtcNow));
        var path = System.IO.Path.Combine(directory.Path, "audit.jsonl");
        File.WriteAllText(path, File.ReadAllText(path).Replace("original", "tampered", StringComparison.Ordinal));

        Assert.False(repository.VerifyAuditIntegrity().Valid);
        Assert.Throws<InvalidDataException>(() => repository.GetAuditEvents("tenant-a"));
        Assert.Throws<InvalidDataException>(() => repository.AppendAudit(Audit("tenant-a", "third", DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void Catalog_AuditRetentionRemovesExpiredEventsAndPreservesVerifiableCheckpoint()
    {
        using var directory = new TemporaryDirectory();
        var options = new CentralCatalogOptions { StorageRoot = directory.Path, AuditRetentionDays = 30 };
        var repository = new FileCentralCatalogRepository(options);
        repository.AppendAudit(Audit("tenant-a", "expired", DateTimeOffset.UtcNow.AddDays(-31)));

        repository.AppendAudit(Audit("tenant-a", "current", DateTimeOffset.UtcNow));

        var events = repository.GetAuditEvents("tenant-a");
        var integrity = repository.VerifyAuditIntegrity();
        Assert.Single(events);
        Assert.Equal("current", events[0].ResourceId);
        Assert.True(integrity.Valid);
        Assert.Equal(2, integrity.EventCount);
        var persisted = File.ReadAllText(System.IO.Path.Combine(directory.Path, "audit.jsonl"));
        Assert.DoesNotContain("expired", persisted, StringComparison.Ordinal);
        Assert.Contains("Audit.RetentionCheckpoint", persisted, StringComparison.Ordinal);

        var reloaded = new FileCentralCatalogRepository(options);
        Assert.True(reloaded.VerifyAuditIntegrity().Valid);
        Assert.Single(reloaded.GetAuditEvents("tenant-a"));
    }

    [Fact]
    public void Catalog_SynchronizesTeamMembershipAndRejectsCrossTenantProjectPaths()
    {
        using var directory = new TemporaryDirectory();
        var repository = new FileCentralCatalogRepository(new CentralCatalogOptions { StorageRoot = directory.Path });
        repository.SaveTenant(Tenant("tenant-a"));
        repository.SaveTenant(Tenant("tenant-b"));
        repository.SaveUser(User("user-a", "tenant-a", CentralRole.Developer));
        repository.SaveTeam(new CentralTeam
        {
            Id = "team-a",
            TenantId = "tenant-a",
            Name = "Automation",
            MemberUserIds = ["user-a"],
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Contains("team-a", Assert.Single(repository.GetSnapshot().Users).TeamIds);
        repository.SaveProject(Project("project-a", "tenant-a", directory.Path));
        var error = Assert.Throws<InvalidOperationException>(() =>
            repository.SaveProject(Project("project-b", "tenant-b", directory.Path)));
        Assert.Contains("another tenant", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_ProfilesAndAnalysisHistoryRemainTenantScoped()
    {
        using var directory = new TemporaryDirectory();
        var repository = new FileCentralCatalogRepository(new CentralCatalogOptions { StorageRoot = directory.Path });
        repository.SaveTenant(Tenant("tenant-a"));
        repository.SaveTenant(Tenant("tenant-b"));
        repository.SaveRuleProfile(new CentralTenantRuleProfile
        {
            TenantId = "tenant-a",
            Profile = new UiPathRuleProfile { Id = "default", Name = "Tenant A Default", Rules = [] },
            UpdatedByUserId = "admin-a",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        var reservation = repository.ReserveAnalysis("tenant-a", "project-a", "user-a", null, "default", 1);
        repository.CompleteAnalysis(reservation with { Score = 90, Grade = "A" });

        Assert.Single(repository.GetRuleProfiles("tenant-a"));
        Assert.Empty(repository.GetRuleProfiles("tenant-b"));
        Assert.Single(repository.GetAnalyses("tenant-a"));
        Assert.Empty(repository.GetAnalyses("tenant-b"));
        Assert.Throws<CentralQuotaExceededException>(() =>
            repository.ReserveAnalysis("tenant-a", "project-a", "user-a", null, "default", 1));
    }

    [Fact]
    public void Catalog_FederatedIdentityUsesProvisionedTenantAndInternalRole()
    {
        using var directory = new TemporaryDirectory();
        var repository = new FileCentralCatalogRepository(new CentralCatalogOptions { StorageRoot = directory.Path });
        repository.SaveTenant(Tenant("tenant-a") with { ExternalIdentityId = "entra-tenant-a" });
        repository.SaveUser(User("user-a", "tenant-a", CentralRole.Manager) with { ExternalSubject = "entra-user-a" });

        var principal = repository.AuthenticateFederated("entra-tenant-a", "entra-user-a");

        Assert.NotNull(principal);
        Assert.Equal(CentralRole.Manager, principal.Role);
        Assert.Null(repository.AuthenticateFederated("entra-tenant-a", "unknown"));
        Assert.Null(repository.AuthenticateFederated("entra-tenant-b", "entra-user-a"));
    }

    private static CentralTenant Tenant(string id) => new()
    {
        Id = id,
        Name = id,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    private static CentralUser User(string id, string tenantId, CentralRole role) => new()
    {
        Id = id,
        TenantId = tenantId,
        Email = $"{id}@example.test",
        DisplayName = id,
        Role = role,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    private static CentralProject Project(string id, string tenantId, string path) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = id,
        ProjectPath = path,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    private static CentralAuditEvent Audit(string tenantId, string resourceId, DateTimeOffset timestamp) => new()
    {
        OperationId = Guid.NewGuid().ToString("N"),
        TimestampUtc = timestamp,
        TenantId = tenantId,
        ActorUserId = "actor",
        Action = "Test",
        ResourceType = "Project",
        ResourceId = resourceId
    };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"RpaDevAssistantCentral-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
            File.WriteAllText(System.IO.Path.Combine(Path, "project.json"), "{\"name\":\"Central Test Project\"}");
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

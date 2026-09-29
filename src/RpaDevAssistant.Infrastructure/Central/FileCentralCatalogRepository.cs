using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RpaDevAssistant.Core.Central;
using RpaDevAssistant.Core.Analysis.Profiles;

namespace RpaDevAssistant.Infrastructure.Central;

public sealed record CentralCatalogOptions
{
    public required string StorageRoot { get; init; }
    public int AuditRetentionDays { get; init; } = 2_555;
}

public sealed class FileCentralCatalogRepository : ICentralCatalogRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonOptions) { WriteIndented = false };

    private readonly object sync = new();
    private readonly string catalogPath;
    private readonly string auditPath;
    private readonly int auditRetentionDays;

    public FileCentralCatalogRepository(CentralCatalogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.StorageRoot))
        {
            throw new ArgumentException("Central catalog storage root is required.", nameof(options));
        }

        var root = Path.GetFullPath(options.StorageRoot);
        if (options.AuditRetentionDays <= 0) throw new ArgumentOutOfRangeException(nameof(options.AuditRetentionDays));
        Directory.CreateDirectory(root);
        catalogPath = Path.Combine(root, "catalog.json");
        auditPath = Path.Combine(root, "audit.jsonl");
        auditRetentionDays = options.AuditRetentionDays;
    }

    public CentralCatalogSnapshot GetSnapshot()
    {
        lock (sync)
        {
            return ToSnapshot(ReadState());
        }
    }

    public CentralPrincipal? Authenticate(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;
        var candidateHash = Hash(apiKey);
        lock (sync)
        {
            var state = ReadState();
            var credential = state.Credentials.FirstOrDefault(item => FixedTimeEquals(item.ApiKeyHash, candidateHash));
            if (credential is null) return null;
            var user = state.Users.FirstOrDefault(item => item.Id == credential.UserId && item.Active);
            var tenant = user is null ? null : state.Tenants.FirstOrDefault(item => item.Id == user.TenantId && item.Active);
            return user is null || tenant is null
                ? null
                : new CentralPrincipal(user.Id, user.TenantId, user.Role, user.DisplayName);
        }
    }

    public CentralPrincipal? AuthenticateFederated(string externalTenantId, string externalSubject)
    {
        if (string.IsNullOrWhiteSpace(externalTenantId) || string.IsNullOrWhiteSpace(externalSubject)) return null;
        lock (sync)
        {
            var state = ReadState();
            var tenant = state.Tenants.FirstOrDefault(item => item.Active
                && string.Equals(item.ExternalIdentityId, externalTenantId, StringComparison.OrdinalIgnoreCase));
            if (tenant is null) return null;
            var user = state.Users.FirstOrDefault(item => item.Active
                && item.TenantId == tenant.Id
                && string.Equals(item.ExternalSubject, externalSubject, StringComparison.Ordinal));
            return user is null ? null : new CentralPrincipal(user.Id, tenant.Id, user.Role, user.DisplayName);
        }
    }

    public CentralTenant SaveTenant(CentralTenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ValidateIdentifier(tenant.Id, nameof(tenant.Id));
        if (string.IsNullOrWhiteSpace(tenant.Name)) throw new ArgumentException("Tenant name is required.", nameof(tenant));
        if (tenant.MonthlyAnalysisQuota <= 0) throw new ArgumentException("Monthly analysis quota must be positive.", nameof(tenant));
        if (tenant.BrandingAccentColor is not null
            && !System.Text.RegularExpressions.Regex.IsMatch(tenant.BrandingAccentColor, "^#[0-9a-fA-F]{6}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentException("Branding accent color must be a six-digit hexadecimal color.", nameof(tenant));

        lock (sync)
        {
            var state = ReadState();
            Upsert(state.Tenants, tenant, item => item.Id);
            WriteState(state);
            return tenant;
        }
    }

    public CentralUserCredential SaveUser(CentralUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        ValidateIdentifier(user.Id, nameof(user.Id));
        ValidateIdentifier(user.TenantId, nameof(user.TenantId));
        if (string.IsNullOrWhiteSpace(user.Email) || !user.Email.Contains('@', StringComparison.Ordinal))
            throw new ArgumentException("A valid user email is required.", nameof(user));
        if (string.IsNullOrWhiteSpace(user.DisplayName)) throw new ArgumentException("User display name is required.", nameof(user));

        lock (sync)
        {
            var state = ReadState();
            if (!state.Tenants.Any(item => item.Id == user.TenantId)) throw new InvalidOperationException("The selected tenant does not exist.");
            if (state.Users.Any(item => item.Id != user.Id && string.Equals(item.Email, user.Email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("The email address is already assigned to another user.");
            if (!string.IsNullOrWhiteSpace(user.ExternalSubject)
                && state.Users.Any(item => item.Id != user.Id && item.TenantId == user.TenantId && item.ExternalSubject == user.ExternalSubject))
                throw new InvalidOperationException("The federated subject is already assigned to another user in this tenant.");
            if (user.TeamIds.Any(id => state.Teams.All(team => team.Id != id || team.TenantId != user.TenantId)))
                throw new InvalidOperationException("Every assigned team must belong to the same tenant.");

            Upsert(state.Users, user, item => item.Id);
            var apiKey = $"rpa_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
            state.Credentials.RemoveAll(item => item.UserId == user.Id);
            state.Credentials.Add(new StoredCredential { UserId = user.Id, ApiKeyHash = Hash(apiKey) });
            WriteState(state);
            return new CentralUserCredential(user, apiKey);
        }
    }

    public CentralTeam SaveTeam(CentralTeam team)
    {
        ArgumentNullException.ThrowIfNull(team);
        ValidateIdentifier(team.Id, nameof(team.Id));
        ValidateIdentifier(team.TenantId, nameof(team.TenantId));
        if (string.IsNullOrWhiteSpace(team.Name)) throw new ArgumentException("Team name is required.", nameof(team));
        lock (sync)
        {
            var state = ReadState();
            EnsureTenant(state, team.TenantId);
            if (team.MemberUserIds.Any(id => state.Users.All(user => user.Id != id || user.TenantId != team.TenantId)))
                throw new InvalidOperationException("Every team member must belong to the same tenant.");
            Upsert(state.Teams, team, item => item.Id);
            for (var index = 0; index < state.Users.Count; index++)
            {
                var user = state.Users[index];
                if (user.TenantId != team.TenantId) continue;
                var teamIds = user.TeamIds.ToHashSet(StringComparer.Ordinal);
                if (team.MemberUserIds.Contains(user.Id, StringComparer.Ordinal)) teamIds.Add(team.Id);
                else teamIds.Remove(team.Id);
                state.Users[index] = user with { TeamIds = teamIds.Order(StringComparer.Ordinal).ToArray() };
            }
            WriteState(state);
            return team;
        }
    }

    public CentralProject SaveProject(CentralProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        ValidateIdentifier(project.Id, nameof(project.Id));
        ValidateIdentifier(project.TenantId, nameof(project.TenantId));
        if (string.IsNullOrWhiteSpace(project.Name)) throw new ArgumentException("Project name is required.", nameof(project));
        if (string.IsNullOrWhiteSpace(project.ProjectPath) || !Path.IsPathFullyQualified(project.ProjectPath))
            throw new ArgumentException("Project path must be absolute.", nameof(project));
        lock (sync)
        {
            var state = ReadState();
            EnsureTenant(state, project.TenantId);
            if (project.TeamId is not null && state.Teams.All(team => team.Id != project.TeamId || team.TenantId != project.TenantId))
                throw new InvalidOperationException("The selected team does not belong to the project tenant.");
            var normalized = project with { ProjectPath = NormalizeProjectRoot(project.ProjectPath) };
            if (state.Projects.Any(item => item.Id != project.Id
                && item.TenantId != project.TenantId
                && string.Equals(NormalizeProjectRoot(item.ProjectPath), normalized.ProjectPath, PathComparison)))
                throw new InvalidOperationException("The project path is already assigned to another tenant.");
            Upsert(state.Projects, normalized, item => item.Id);
            WriteState(state);
            return normalized;
        }
    }

    public CentralAnalysisRecord ReserveAnalysis(string tenantId, string projectId, string userId, string? teamId, string profileId, int monthlyQuota)
    {
        ValidateIdentifier(tenantId, nameof(tenantId));
        ValidateIdentifier(projectId, nameof(projectId));
        ValidateIdentifier(userId, nameof(userId));
        if (monthlyQuota <= 0) throw new ArgumentOutOfRangeException(nameof(monthlyQuota));
        lock (sync)
        {
            var state = ReadState();
            EnsureTenant(state, tenantId);
            var monthStart = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var usage = state.Analyses.Count(item => item.TenantId == tenantId && item.StartedAtUtc >= monthStart);
            if (usage >= monthlyQuota) throw new CentralQuotaExceededException(monthlyQuota);
            var analysis = new CentralAnalysisRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                ProjectId = projectId,
                UserId = userId,
                TeamId = teamId,
                ProfileId = profileId,
                Status = CentralAnalysisStatus.Running,
                StartedAtUtc = DateTimeOffset.UtcNow
            };
            state.Analyses.Add(analysis);
            WriteState(state);
            return analysis;
        }
    }

    public CentralAnalysisRecord CompleteAnalysis(CentralAnalysisRecord analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        lock (sync)
        {
            var state = ReadState();
            if (state.Analyses.All(item => item.Id != analysis.Id)) throw new InvalidOperationException("The analysis reservation does not exist.");
            var completed = analysis with { Status = CentralAnalysisStatus.Completed, CompletedAtUtc = analysis.CompletedAtUtc ?? DateTimeOffset.UtcNow, FailureCode = null };
            Upsert(state.Analyses, completed, item => item.Id);
            WriteState(state);
            return completed;
        }
    }

    public CentralAnalysisRecord FailAnalysis(string analysisId, string failureCode)
    {
        ValidateIdentifier(analysisId, nameof(analysisId));
        lock (sync)
        {
            var state = ReadState();
            var analysis = state.Analyses.FirstOrDefault(item => item.Id == analysisId)
                ?? throw new InvalidOperationException("The analysis reservation does not exist.");
            var failed = analysis with
            {
                Status = CentralAnalysisStatus.Failed,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                FailureCode = string.IsNullOrWhiteSpace(failureCode) ? "ANALYSIS_FAILED" : failureCode
            };
            Upsert(state.Analyses, failed, item => item.Id);
            WriteState(state);
            return failed;
        }
    }

    public IReadOnlyList<CentralAnalysisRecord> GetAnalyses(string tenantId, string? projectId = null, int limit = 500)
    {
        ValidateIdentifier(tenantId, nameof(tenantId));
        if (limit is < 1 or > 5_000) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (sync)
        {
            return ReadState().Analyses
                .Where(item => item.TenantId == tenantId && (projectId is null || item.ProjectId == projectId))
                .OrderByDescending(item => item.StartedAtUtc)
                .Take(limit)
                .ToArray();
        }
    }

    public CentralTenantRuleProfile SaveRuleProfile(CentralTenantRuleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateIdentifier(profile.TenantId, nameof(profile.TenantId));
        var errors = UiPathRuleProfileValidation.Validate(profile.Profile, protectBuiltInDefault: false);
        if (errors.Count > 0) throw new UiPathRuleProfileValidationException(errors);
        lock (sync)
        {
            var state = ReadState();
            EnsureTenant(state, profile.TenantId);
            var normalized = profile with { UpdatedAtUtc = DateTimeOffset.UtcNow };
            var index = state.RuleProfiles.FindIndex(item => item.TenantId == profile.TenantId
                && item.Profile.Id.Equals(profile.Profile.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) state.RuleProfiles[index] = normalized;
            else state.RuleProfiles.Add(normalized);
            WriteState(state);
            return normalized;
        }
    }

    public IReadOnlyList<CentralTenantRuleProfile> GetRuleProfiles(string tenantId)
    {
        ValidateIdentifier(tenantId, nameof(tenantId));
        lock (sync)
        {
            return ReadState().RuleProfiles
                .Where(item => item.TenantId == tenantId)
                .OrderBy(item => item.Profile.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public void AppendAudit(CentralAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        lock (sync)
        {
            var chain = ApplyAuditRetention(ReadAuditChain());
            if (!chain.Integrity.Valid) throw new InvalidDataException("The central audit chain failed integrity validation and was not modified.");
            var previousHash = chain.Integrity.LastHash ?? string.Empty;
            var envelope = new StoredAuditEnvelope
            {
                Event = auditEvent,
                PreviousHash = previousHash,
                Hash = AuditHash(previousHash, auditEvent)
            };
            File.AppendAllText(auditPath, JsonSerializer.Serialize(envelope, AuditJsonOptions) + Environment.NewLine, new UTF8Encoding(false));
        }
    }

    public IReadOnlyList<CentralAuditEvent> GetAuditEvents(string tenantId, int limit = 200)
    {
        ValidateIdentifier(tenantId, nameof(tenantId));
        if (limit is < 1 or > 1_000) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (sync)
        {
            var chain = ReadAuditChain();
            if (!chain.Integrity.Valid) throw new InvalidDataException(chain.Integrity.Error ?? "The central audit chain is invalid.");
            return chain.Events
                .Where(item => item.TenantId == tenantId)
                .OrderByDescending(item => item.TimestampUtc)
                .Take(limit)
                .ToArray();
        }
    }

    public CentralAuditIntegrity VerifyAuditIntegrity()
    {
        lock (sync)
        {
            return ReadAuditChain().Integrity;
        }
    }

    private AuditChain ReadAuditChain()
    {
        if (!File.Exists(auditPath)) return new AuditChain([], new CentralAuditIntegrity(true, 0, 0, null, null));
        var events = new List<CentralAuditEvent>();
        var previousHash = string.Empty;
        var legacyCount = 0;
        var lineNumber = 0;
        foreach (var line in File.ReadLines(auditPath).Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            lineNumber++;
            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("event", out _))
                {
                    var envelope = JsonSerializer.Deserialize<StoredAuditEnvelope>(line, AuditJsonOptions)
                        ?? throw new JsonException("Audit envelope is empty.");
                    var expected = AuditHash(previousHash, envelope.Event);
                    if (!FixedTimeEquals(envelope.PreviousHash, previousHash) || !FixedTimeEquals(envelope.Hash, expected))
                        return Invalid($"Audit hash mismatch at line {lineNumber}.");
                    previousHash = envelope.Hash;
                    events.Add(envelope.Event);
                }
                else
                {
                    var legacy = JsonSerializer.Deserialize<CentralAuditEvent>(line, AuditJsonOptions)
                        ?? throw new JsonException("Audit event is empty.");
                    previousHash = AuditHash(previousHash, legacy);
                    legacyCount++;
                    events.Add(legacy);
                }
            }
            catch (JsonException)
            {
                return Invalid($"Audit JSON is invalid at line {lineNumber}.");
            }
        }

        return new AuditChain(events, new CentralAuditIntegrity(true, events.Count, legacyCount, previousHash.Length == 0 ? null : previousHash, null));

        AuditChain Invalid(string error) => new(events, new CentralAuditIntegrity(false, events.Count, legacyCount, previousHash.Length == 0 ? null : previousHash, error));
    }

    private AuditChain ApplyAuditRetention(AuditChain chain)
    {
        if (!chain.Integrity.Valid) return chain;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-auditRetentionDays);
        var retained = chain.Events.Where(item => item.TimestampUtc >= cutoff).ToArray();
        var removedCount = chain.Events.Count - retained.Length;
        if (removedCount == 0) return chain;

        var checkpoint = new CentralAuditEvent
        {
            OperationId = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTimeOffset.UtcNow,
            TenantId = "system",
            ActorUserId = "retention",
            Action = "Audit.RetentionCheckpoint",
            ResourceType = "AuditChain",
            ResourceId = chain.Integrity.LastHash ?? "empty",
            Outcome = $"Removed:{removedCount}"
        };
        WriteAuditChain([checkpoint, .. retained]);
        return ReadAuditChain();
    }

    private void WriteAuditChain(IReadOnlyList<CentralAuditEvent> events)
    {
        var previousHash = string.Empty;
        var lines = new List<string>(events.Count);
        foreach (var auditEvent in events)
        {
            var hash = AuditHash(previousHash, auditEvent);
            lines.Add(JsonSerializer.Serialize(new StoredAuditEnvelope
            {
                Event = auditEvent,
                PreviousHash = previousHash,
                Hash = hash
            }, AuditJsonOptions));
            previousHash = hash;
        }

        var tempPath = auditPath + $".{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, string.Join(Environment.NewLine, lines) + Environment.NewLine, new UTF8Encoding(false));
        try { File.Move(tempPath, auditPath, overwrite: true); }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }

    private static string AuditHash(string previousHash, CentralAuditEvent auditEvent)
    {
        var canonical = previousHash + "\n" + JsonSerializer.Serialize(auditEvent, AuditJsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private CatalogState ReadState()
    {
        if (!File.Exists(catalogPath)) return new CatalogState();
        try
        {
            return JsonSerializer.Deserialize<CatalogState>(File.ReadAllText(catalogPath), JsonOptions) ?? new CatalogState();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The central catalog is invalid and was not modified.", ex);
        }
    }

    private void WriteState(CatalogState state)
    {
        var tempPath = catalogPath + $".{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(state, JsonOptions), new UTF8Encoding(false));
        try
        {
            File.Move(tempPath, catalogPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static CentralCatalogSnapshot ToSnapshot(CatalogState state) => new()
    {
        Tenants = state.Tenants.ToArray(),
        Users = state.Users.ToArray(),
        Teams = state.Teams.ToArray(),
        Projects = state.Projects.ToArray(),
        Analyses = state.Analyses.ToArray(),
        RuleProfiles = state.RuleProfiles.ToArray()
    };

    private static void EnsureTenant(CatalogState state, string tenantId)
    {
        if (!state.Tenants.Any(item => item.Id == tenantId)) throw new InvalidOperationException("The selected tenant does not exist.");
    }

    private static void Upsert<T>(List<T> items, T value, Func<T, string> id)
    {
        var index = items.FindIndex(item => id(item) == id(value));
        if (index >= 0) items[index] = value;
        else items.Add(value);
    }

    private static void ValidateIdentifier(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 80 || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
            throw new ArgumentException("Identifiers may contain only ASCII letters, digits, '-' and '_'.", name);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool FixedTimeEquals(string left, string right)
    {
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right)); }
        catch (FormatException) { return false; }
    }

    private static string NormalizeProjectRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath) || !File.Exists(Path.Combine(fullPath, "project.json")))
            throw new DirectoryNotFoundException("The registered project path must be an existing UiPath project root containing project.json.");
        var resolved = new DirectoryInfo(fullPath).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? fullPath;
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(resolved));
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private sealed class CatalogState
    {
        public List<CentralTenant> Tenants { get; init; } = [];
        public List<CentralUser> Users { get; init; } = [];
        public List<CentralTeam> Teams { get; init; } = [];
        public List<CentralProject> Projects { get; init; } = [];
        public List<CentralAnalysisRecord> Analyses { get; init; } = [];
        public List<CentralTenantRuleProfile> RuleProfiles { get; init; } = [];
        public List<StoredCredential> Credentials { get; init; } = [];
    }

    private sealed record StoredCredential
    {
        public required string UserId { get; init; }
        public required string ApiKeyHash { get; init; }
    }

    private sealed record StoredAuditEnvelope
    {
        public required CentralAuditEvent Event { get; init; }
        public required string PreviousHash { get; init; }
        public required string Hash { get; init; }
    }

    private sealed record AuditChain(IReadOnlyList<CentralAuditEvent> Events, CentralAuditIntegrity Integrity);
}

public sealed class CentralQuotaExceededException(int quota)
    : InvalidOperationException($"The monthly analysis quota of {quota} has been reached.");

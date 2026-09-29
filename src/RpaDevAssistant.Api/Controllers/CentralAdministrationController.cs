using Microsoft.AspNetCore.Mvc;
using RpaDevAssistant.Api.Configuration;
using RpaDevAssistant.Api.Services;
using RpaDevAssistant.Core.Central;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Reporting;
using RpaDevAssistant.Core.Reporting.Export;
using System.Text;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/central")]
public sealed class CentralAdministrationController(
    RpaDevAssistantCentralOptions options,
    ICentralCatalogRepository repository,
    IUiPathProjectAnalyzer projectAnalyzer,
    IUiPathRuleProfileProvider profileProvider,
    ICentralLicenseService licenseService,
    IUiPathAnalysisReportService reportService,
    IUiPathReportExportService reportExportService) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() => Ok(new { enabled = options.Enabled, authentication = options.Enabled ? options.AuthenticationMode.ToString() : "None", license = licenseService.GetStatus() });

    [HttpGet("catalog")]
    public IActionResult Catalog()
    {
        var principal = Principal();
        var snapshot = repository.GetSnapshot();
        if (principal.Role == CentralRole.SystemAdmin) return Ok(snapshot);
        return Ok(new CentralCatalogSnapshot
        {
            Tenants = snapshot.Tenants.Where(item => item.Id == principal.TenantId).ToArray(),
            Users = snapshot.Users.Where(item => item.TenantId == principal.TenantId).ToArray(),
            Teams = snapshot.Teams.Where(item => item.TenantId == principal.TenantId).ToArray(),
            Projects = VisibleProjects(snapshot, principal)
        });
    }

    [HttpPost("tenants")]
    public IActionResult SaveTenant([FromBody] SaveTenantRequest request)
    {
        var principal = RequireRole(CentralRole.SystemAdmin);
        var candidate = new CentralTenant
        {
            Id = request.Id,
            Name = request.Name,
            Active = request.Active,
            Plan = request.Plan,
            MonthlyAnalysisQuota = request.MonthlyAnalysisQuota,
            BrandingName = request.BrandingName,
            BrandingAccentColor = request.BrandingAccentColor,
            ExternalIdentityId = request.ExternalIdentityId,
            CreatedAtUtc = request.CreatedAtUtc ?? DateTimeOffset.UtcNow
        };
        licenseService.ValidateTenantChange(repository.GetSnapshot().Tenants, candidate);
        var tenant = repository.SaveTenant(candidate);
        Audit(principal, tenant.Id, "Tenant.Save", "Tenant", tenant.Id);
        return Ok(tenant);
    }

    [HttpPost("users")]
    public IActionResult SaveUser([FromBody] SaveUserRequest request)
    {
        var principal = RequireRole(CentralRole.TenantAdmin);
        EnsureTenantAccess(principal, request.TenantId);
        if (principal.Role != CentralRole.SystemAdmin && request.Role is CentralRole.SystemAdmin)
            return Forbid();

        var credential = repository.SaveUser(new CentralUser
        {
            Id = request.Id,
            TenantId = request.TenantId,
            Email = request.Email,
            DisplayName = request.DisplayName,
            Role = request.Role,
            Active = request.Active,
            TeamIds = request.TeamIds ?? [],
            ExternalSubject = request.ExternalSubject,
            CreatedAtUtc = request.CreatedAtUtc ?? DateTimeOffset.UtcNow
        });
        Audit(principal, request.TenantId, "User.Save", "User", request.Id);
        return Ok(credential);
    }

    [HttpPost("teams")]
    public IActionResult SaveTeam([FromBody] SaveTeamRequest request)
    {
        var principal = RequireRole(CentralRole.TenantAdmin);
        EnsureTenantAccess(principal, request.TenantId);
        var team = repository.SaveTeam(new CentralTeam
        {
            Id = request.Id,
            TenantId = request.TenantId,
            Name = request.Name,
            MemberUserIds = request.MemberUserIds ?? [],
            CreatedAtUtc = request.CreatedAtUtc ?? DateTimeOffset.UtcNow
        });
        Audit(principal, request.TenantId, "Team.Save", "Team", request.Id);
        return Ok(team);
    }

    [HttpPost("projects")]
    public IActionResult SaveProject([FromBody] SaveProjectRequest request)
    {
        var principal = RequireRole(CentralRole.Manager);
        EnsureTenantAccess(principal, request.TenantId);
        var profileId = string.IsNullOrWhiteSpace(request.RuleProfileId) ? "default" : request.RuleProfileId;
        EnsureProfileExists(request.TenantId, profileId);
        var project = repository.SaveProject(new CentralProject
        {
            Id = request.Id,
            TenantId = request.TenantId,
            Name = request.Name,
            ProjectPath = request.ProjectPath,
            RuleProfileId = profileId,
            TeamId = request.TeamId,
            Active = request.Active,
            CreatedAtUtc = request.CreatedAtUtc ?? DateTimeOffset.UtcNow
        });
        Audit(principal, request.TenantId, "Project.Save", "Project", request.Id);
        return Ok(project);
    }

    [HttpGet("rule-profiles")]
    public IActionResult RuleProfiles([FromQuery] string? tenantId)
    {
        var principal = ScopePrincipal(Principal(), tenantId);
        var tenantProfiles = repository.GetRuleProfiles(principal.TenantId);
        var overriddenIds = tenantProfiles.Select(item => item.Profile.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var effective = tenantProfiles.Select(item => new { source = "Tenant", item.Profile, item.UpdatedAtUtc, item.UpdatedByUserId })
            .Concat(profileProvider.GetProfiles()
                .Where(profile => !overriddenIds.Contains(profile.Id))
                .Select(profile => new { source = "BuiltIn", Profile = profile, UpdatedAtUtc = DateTimeOffset.MinValue, UpdatedByUserId = string.Empty }))
            .OrderBy(item => item.Profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Ok(new { tenantId = principal.TenantId, profiles = effective });
    }

    [HttpPost("rule-profiles")]
    public IActionResult SaveRuleProfile([FromQuery] string? tenantId, [FromBody] UiPathRuleProfile profile)
    {
        var principal = RequireRole(CentralRole.TenantAdmin);
        var scopedPrincipal = ScopePrincipal(principal, tenantId);
        var saved = repository.SaveRuleProfile(new CentralTenantRuleProfile
        {
            TenantId = scopedPrincipal.TenantId,
            Profile = profile,
            UpdatedByUserId = principal.UserId,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        Audit(principal, scopedPrincipal.TenantId, "RuleProfile.Save", "RuleProfile", profile.Id);
        return Ok(saved);
    }

    [HttpGet("audit")]
    public IActionResult AuditEvents([FromQuery] string? tenantId, [FromQuery] int limit = 200)
    {
        var principal = RequireRole(CentralRole.TenantAdmin);
        var selectedTenant = principal.Role == CentralRole.SystemAdmin ? tenantId : principal.TenantId;
        if (string.IsNullOrWhiteSpace(selectedTenant)) return BadRequest(new { error = "TENANT_REQUIRED" });
        EnsureTenantAccess(principal, selectedTenant);
        return Ok(new { events = repository.GetAuditEvents(selectedTenant, limit) });
    }

    [HttpGet("audit/export")]
    public IActionResult ExportAudit([FromQuery] string? tenantId, [FromQuery] int limit = 1_000)
    {
        var principal = RequireRole(CentralRole.TenantAdmin);
        var selectedTenant = principal.Role == CentralRole.SystemAdmin ? tenantId : principal.TenantId;
        if (string.IsNullOrWhiteSpace(selectedTenant)) return BadRequest(new { error = "TENANT_REQUIRED" });
        EnsureTenantAccess(principal, selectedTenant);
        var integrity = repository.VerifyAuditIntegrity();
        if (!integrity.Valid) return Conflict(new { error = "AUDIT_INTEGRITY_FAILED", integrity });
        return Ok(new { schemaVersion = "1.0", tenantId = selectedTenant, exportedAtUtc = DateTimeOffset.UtcNow, integrity, events = repository.GetAuditEvents(selectedTenant, limit) });
    }

    [HttpPost("projects/{projectId}/analyze")]
    public async Task<IActionResult> AnalyzeProject(string projectId, CancellationToken cancellationToken)
    {
        var principal = RequireRole(CentralRole.Developer);
        var snapshot = repository.GetSnapshot();
        var project = ResolveVisibleProject(snapshot, principal, projectId);
        if (project is null) return NotFound(new { error = "PROJECT_NOT_FOUND" });
        var tenant = snapshot.Tenants.First(item => item.Id == project.TenantId);
        var reservation = repository.ReserveAnalysis(
            project.TenantId,
            project.Id,
            principal.UserId,
            project.TeamId,
            project.RuleProfileId,
            tenant.MonthlyAnalysisQuota);

        try
        {
            var tenantProfile = repository.GetRuleProfiles(project.TenantId)
                .FirstOrDefault(item => item.Profile.Id.Equals(project.RuleProfileId, StringComparison.OrdinalIgnoreCase))?.Profile;
            var result = tenantProfile is null
                ? await projectAnalyzer.AnalyzeAsync(project.ProjectPath, project.RuleProfileId, cancellationToken)
                : await projectAnalyzer.AnalyzeAsync(project.ProjectPath, tenantProfile, cancellationToken);
            var completed = repository.CompleteAnalysis(reservation with
            {
                Score = result.QualityScore.Score,
                Grade = result.QualityScore.Grade,
                WorkflowCount = result.WorkflowCount,
                ActivityCount = result.TotalActivityCount,
                FindingCount = result.Analysis.TotalFindings,
                CriticalCount = result.Analysis.CriticalCount,
                ErrorCount = result.Analysis.ErrorCount,
                WarningCount = result.Analysis.WarningCount,
                SuggestionCount = result.Analysis.SuggestionCount
            });
            Audit(principal, project.TenantId, "Analysis.Complete", "Analysis", completed.Id);
            return Ok(new { analysisId = completed.Id, projectId = project.Id, result });
        }
        catch
        {
            repository.FailAnalysis(reservation.Id, "ANALYSIS_FAILED");
            Audit(principal, project.TenantId, "Analysis.Fail", "Analysis", reservation.Id);
            throw;
        }
    }

    [HttpGet("history")]
    public IActionResult History([FromQuery] string? tenantId, [FromQuery] string? projectId, [FromQuery] int limit = 500)
    {
        var principal = Principal();
        var scopedPrincipal = ScopePrincipal(principal, tenantId);
        var snapshot = repository.GetSnapshot();
        var visibleProjectIds = VisibleProjects(snapshot, scopedPrincipal).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        if (projectId is not null && !visibleProjectIds.Contains(projectId)) return NotFound(new { error = "PROJECT_NOT_FOUND" });
        var analyses = repository.GetAnalyses(scopedPrincipal.TenantId, projectId, limit)
            .Where(item => visibleProjectIds.Contains(item.ProjectId))
            .ToArray();
        return Ok(new { analyses });
    }

    [HttpPost("projects/{projectId}/report")]
    public async Task<IActionResult> ExportProjectReport(string projectId, [FromBody] CentralReportRequest request, CancellationToken cancellationToken)
    {
        var principal = RequireRole(CentralRole.Developer);
        var snapshot = repository.GetSnapshot();
        var project = ResolveVisibleProject(snapshot, principal, projectId);
        if (project is null) return NotFound(new { error = "PROJECT_NOT_FOUND" });
        if (!Enum.TryParse<UiPathReportExportFormat>(request.Format, true, out var format))
            return BadRequest(new { error = "format must be json, html, or pdf." });
        var tenant = snapshot.Tenants.First(item => item.Id == project.TenantId);
        var reservation = repository.ReserveAnalysis(
            project.TenantId,
            project.Id,
            principal.UserId,
            project.TeamId,
            project.RuleProfileId,
            tenant.MonthlyAnalysisQuota);

        try
        {
            var tenantProfile = repository.GetRuleProfiles(project.TenantId)
                .FirstOrDefault(item => item.Profile.Id.Equals(project.RuleProfileId, StringComparison.OrdinalIgnoreCase))?.Profile;
            var report = await reportService.GenerateAsync(new UiPathReportGenerationOptions
            {
                ProjectPath = project.ProjectPath,
                ProfileId = project.RuleProfileId,
                Profile = tenantProfile,
                Locale = request.Locale,
                Branding = new UiPathReportBranding
                {
                    CompanyName = tenant.BrandingName ?? tenant.Name,
                    AccentColor = tenant.BrandingAccentColor
                }
            }, cancellationToken);
            var export = reportExportService.Export(report, format, request.Locale);
            var completed = repository.CompleteAnalysis(reservation with
            {
                Score = report.QualityScore,
                Grade = report.Grade,
                WorkflowCount = report.WorkflowCount,
                ActivityCount = report.TotalActivityCount,
                FindingCount = report.Summary.TotalFindings,
                CriticalCount = report.Summary.CriticalCount,
                ErrorCount = report.Summary.ErrorCount,
                WarningCount = report.Summary.WarningCount,
                SuggestionCount = report.Summary.SuggestionCount
            });
            Audit(principal, project.TenantId, "Analysis.Complete", "Analysis", completed.Id);
            Audit(principal, project.TenantId, "Report.Export", "Project", project.Id);
            return File(Encoding.UTF8.GetBytes(export.Content), export.ContentType, export.FileName);
        }
        catch
        {
            repository.FailAnalysis(reservation.Id, "REPORT_GENERATION_FAILED");
            Audit(principal, project.TenantId, "Report.Export.Fail", "Analysis", reservation.Id, "Failed");
            throw;
        }
    }

    [HttpGet("dashboard")]
    public IActionResult Dashboard([FromQuery] string? tenantId)
    {
        var principal = Principal();
        var scopedPrincipal = ScopePrincipal(principal, tenantId);
        var snapshot = repository.GetSnapshot();
        var projects = VisibleProjects(snapshot, scopedPrincipal);
        var projectIds = projects.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var completed = repository.GetAnalyses(scopedPrincipal.TenantId, limit: 5_000)
            .Where(item => item.Status == CentralAnalysisStatus.Completed && projectIds.Contains(item.ProjectId))
            .ToArray();
        var latestByProject = completed
            .GroupBy(item => item.ProjectId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.CompletedAtUtc).First())
            .ToArray();
        var tenant = snapshot.Tenants.FirstOrDefault(item => item.Id == scopedPrincipal.TenantId);
        var monthStart = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthlyUsage = repository.GetAnalyses(scopedPrincipal.TenantId, limit: 5_000).Count(item => item.StartedAtUtc >= monthStart);

        return Ok(new
        {
            tenantId = scopedPrincipal.TenantId,
            projectCount = projects.Count,
            analyzedProjectCount = latestByProject.Length,
            averageScore = latestByProject.Length == 0 ? (double?)null : Math.Round(latestByProject.Average(item => item.Score ?? 0), 1),
            monthlyUsage,
            monthlyQuota = tenant?.MonthlyAnalysisQuota,
            projects = projects.Select(project => new
            {
                project.Id,
                project.Name,
                project.TeamId,
                latest = latestByProject.FirstOrDefault(item => item.ProjectId == project.Id),
                trend = completed.Where(item => item.ProjectId == project.Id)
                    .OrderBy(item => item.CompletedAtUtc)
                    .Select(item => new { item.CompletedAtUtc, item.Score, item.Grade, item.FindingCount })
                    .ToArray()
            }),
            teams = completed.GroupBy(item => item.TeamId ?? "unassigned", StringComparer.Ordinal)
                .Select(group => new { teamId = group.Key, analysisCount = group.Count(), averageScore = Math.Round(group.Average(item => item.Score ?? 0), 1) }),
            developers = completed.GroupBy(item => item.UserId, StringComparer.Ordinal)
                .Select(group => new { userId = group.Key, analysisCount = group.Count(), averageScore = Math.Round(group.Average(item => item.Score ?? 0), 1) })
        });
    }

    private CentralPrincipal Principal() =>
        HttpContext.Items[CentralAccessMiddleware.PrincipalItemKey] as CentralPrincipal
        ?? throw new InvalidOperationException("Central principal was not resolved.");

    private CentralPrincipal RequireRole(CentralRole minimum)
    {
        var principal = Principal();
        if (principal.Role < minimum) throw new CentralAccessDeniedException();
        return principal;
    }

    private static void EnsureTenantAccess(CentralPrincipal principal, string tenantId)
    {
        if (principal.Role != CentralRole.SystemAdmin && principal.TenantId != tenantId)
            throw new CentralAccessDeniedException();
    }

    private static IReadOnlyList<CentralProject> VisibleProjects(CentralCatalogSnapshot snapshot, CentralPrincipal principal)
    {
        var tenantProjects = snapshot.Projects.Where(item => item.TenantId == principal.TenantId);
        if (principal.Role >= CentralRole.Manager) return tenantProjects.ToArray();
        var user = snapshot.Users.FirstOrDefault(item => item.Id == principal.UserId);
        var teamIds = user?.TeamIds.ToHashSet(StringComparer.Ordinal) ?? [];
        return tenantProjects.Where(item => item.TeamId is null || teamIds.Contains(item.TeamId)).ToArray();
    }

    private static CentralProject? ResolveVisibleProject(CentralCatalogSnapshot snapshot, CentralPrincipal principal, string projectId) =>
        principal.Role == CentralRole.SystemAdmin
            ? snapshot.Projects.FirstOrDefault(item => item.Id == projectId && item.Active)
            : VisibleProjects(snapshot, principal).FirstOrDefault(item => item.Id == projectId && item.Active);

    private static CentralPrincipal ScopePrincipal(CentralPrincipal principal, string? tenantId)
    {
        if (principal.Role != CentralRole.SystemAdmin) return principal;
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("tenantId is required for system-wide history and dashboard queries.");
        return principal with { TenantId = tenantId };
    }

    private void EnsureProfileExists(string tenantId, string profileId)
    {
        if (repository.GetRuleProfiles(tenantId).Any(item => item.Profile.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase))) return;
        try { _ = profileProvider.GetProfile(profileId); }
        catch (UnknownRuleProfileException) { throw new ArgumentException($"Rule profile '{profileId}' does not exist for this tenant."); }
    }

    private void Audit(CentralPrincipal principal, string tenantId, string action, string resourceType, string resourceId, string outcome = "Success") =>
        repository.AppendAudit(new CentralAuditEvent
        {
            OperationId = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTimeOffset.UtcNow,
            TenantId = tenantId,
            ActorUserId = principal.UserId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Outcome = outcome
        });

    public sealed record SaveTenantRequest(string Id, string Name, bool Active = true, CentralSubscriptionPlan Plan = CentralSubscriptionPlan.Internal, int MonthlyAnalysisQuota = 1_000, string? BrandingName = null, string? BrandingAccentColor = null, string? ExternalIdentityId = null, DateTimeOffset? CreatedAtUtc = null);
    public sealed record SaveUserRequest(string Id, string TenantId, string Email, string DisplayName, CentralRole Role, bool Active = true, IReadOnlyList<string>? TeamIds = null, string? ExternalSubject = null, DateTimeOffset? CreatedAtUtc = null);
    public sealed record SaveTeamRequest(string Id, string TenantId, string Name, IReadOnlyList<string>? MemberUserIds = null, DateTimeOffset? CreatedAtUtc = null);
    public sealed record SaveProjectRequest(string Id, string TenantId, string Name, string ProjectPath, string? RuleProfileId = null, string? TeamId = null, bool Active = true, DateTimeOffset? CreatedAtUtc = null);
    public sealed record CentralReportRequest(string Format = "html", string Locale = "en");
}

public sealed class CentralAccessDeniedException : Exception;

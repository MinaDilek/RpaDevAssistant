namespace RpaDevAssistant.Core.Central;

public interface ICentralCatalogRepository
{
    CentralCatalogSnapshot GetSnapshot();

    CentralPrincipal? Authenticate(string apiKey);

    CentralPrincipal? AuthenticateFederated(string externalTenantId, string externalSubject);

    CentralTenant SaveTenant(CentralTenant tenant);

    CentralUserCredential SaveUser(CentralUser user);

    CentralUser ProvisionFederatedUser(CentralUser user);

    CentralTeam SaveTeam(CentralTeam team);

    CentralProject SaveProject(CentralProject project);

    CentralAnalysisRecord ReserveAnalysis(string tenantId, string projectId, string userId, string? teamId, string profileId, int monthlyQuota);

    CentralAnalysisRecord CompleteAnalysis(CentralAnalysisRecord analysis);

    CentralAnalysisRecord FailAnalysis(string analysisId, string failureCode);

    IReadOnlyList<CentralAnalysisRecord> GetAnalyses(string tenantId, string? projectId = null, int limit = 500);

    CentralTenantRuleProfile SaveRuleProfile(CentralTenantRuleProfile profile);

    IReadOnlyList<CentralTenantRuleProfile> GetRuleProfiles(string tenantId);

    void AppendAudit(CentralAuditEvent auditEvent);

    IReadOnlyList<CentralAuditEvent> GetAuditEvents(string tenantId, int limit = 200);

    CentralAuditIntegrity VerifyAuditIntegrity();
}

import { getBackendBaseUrl } from './apiClient';

export type CentralRole = 'Viewer' | 'Developer' | 'Manager' | 'TenantAdmin' | 'SystemAdmin';

export interface CentralTenant { id: string; name: string; active: boolean; plan: string; monthlyAnalysisQuota: number; brandingName?: string; brandingAccentColor?: string; }
export interface CentralUser { id: string; tenantId: string; email: string; displayName: string; role: CentralRole; active: boolean; teamIds: string[]; }
export interface CentralTeam { id: string; tenantId: string; name: string; memberUserIds: string[]; }
export interface CentralProject { id: string; tenantId: string; name: string; projectPath: string; ruleProfileId: string; teamId?: string; active: boolean; }
export interface CentralAnalysis { id: string; projectId: string; userId: string; status: string; startedAtUtc: string; completedAtUtc?: string; score?: number; grade?: string; findingCount?: number; }
export interface CentralAuditEvent { operationId: string; timestampUtc: string; actorUserId: string; action: string; resourceType: string; resourceId: string; outcome: string; }
export interface CentralCatalog { tenants: CentralTenant[]; users: CentralUser[]; teams: CentralTeam[]; projects: CentralProject[]; analyses: CentralAnalysis[]; ruleProfiles: unknown[]; }
export interface CentralDashboard {
  tenantId: string;
  projectCount: number;
  analyzedProjectCount: number;
  averageScore?: number;
  monthlyUsage: number;
  monthlyQuota?: number;
  projects: Array<{ id: string; name: string; teamId?: string; latest?: CentralAnalysis; trend: Array<{ completedAtUtc?: string; score?: number; grade?: string; findingCount?: number }> }>;
  teams: Array<{ teamId: string; analysisCount: number; averageScore: number }>;
  developers: Array<{ userId: string; analysisCount: number; averageScore: number }>;
}

export interface CentralStatus { enabled: boolean; authentication: string; license?: { state: string; plan?: string; expiresAtUtc?: string; error?: string; allowsAccess: boolean } }

export async function getCentralStatus(): Promise<CentralStatus> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/central/status`);
  if (response.status === 404) return { enabled: false, authentication: 'None' };
  return readJson(response);
}

export async function getCentralCatalog(token: string): Promise<CentralCatalog> {
  return centralRequest('/api/central/catalog', token);
}

export async function getCentralDashboard(token: string, tenantId?: string): Promise<CentralDashboard> {
  return centralRequest(`/api/central/dashboard${query({ tenantId })}`, token);
}

export async function getCentralHistory(token: string, tenantId?: string): Promise<{ analyses: CentralAnalysis[] }> {
  return centralRequest(`/api/central/history${query({ tenantId })}`, token);
}

export async function getCentralAudit(token: string, tenantId?: string): Promise<{ events: CentralAuditEvent[] }> {
  return centralRequest(`/api/central/audit${query({ tenantId })}`, token);
}

export async function getCentralRuleProfiles(token: string, tenantId?: string): Promise<{ tenantId: string; profiles: Array<{ source: string; profile: Record<string, unknown> }> }> {
  return centralRequest(`/api/central/rule-profiles${query({ tenantId })}`, token);
}

export async function saveCentralRuleProfile(token: string, profile: Record<string, unknown>, tenantId?: string): Promise<unknown> {
  return centralRequest(`/api/central/rule-profiles${query({ tenantId })}`, token, profile);
}

export async function saveCentralTenant(token: string, tenant: Partial<CentralTenant> & Pick<CentralTenant, 'id' | 'name'>): Promise<CentralTenant> {
  return centralRequest('/api/central/tenants', token, tenant);
}

export async function saveCentralUser(token: string, user: Partial<CentralUser> & Pick<CentralUser, 'id' | 'tenantId' | 'email' | 'displayName' | 'role'>): Promise<{ user: CentralUser; apiKey: string }> {
  return centralRequest('/api/central/users', token, user);
}

export async function saveCentralTeam(token: string, team: Partial<CentralTeam> & Pick<CentralTeam, 'id' | 'tenantId' | 'name'>): Promise<CentralTeam> {
  return centralRequest('/api/central/teams', token, team);
}

export async function saveCentralProject(token: string, project: Partial<CentralProject> & Pick<CentralProject, 'id' | 'tenantId' | 'name' | 'projectPath'>): Promise<CentralProject> {
  return centralRequest('/api/central/projects', token, project);
}

export async function runCentralAnalysis(token: string, projectId: string): Promise<unknown> {
  return centralRequest(`/api/central/projects/${encodeURIComponent(projectId)}/analyze`, token, {});
}

async function centralRequest<T>(path: string, token: string, body?: unknown): Promise<T> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}${path}`, {
    method: body === undefined ? 'GET' : 'POST',
    headers: {
      Authorization: `Bearer ${token.trim()}`,
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  return readJson(response);
}

async function readJson<T>(response: Response): Promise<T> {
  const result = await response.json().catch(() => ({}));
  if (!response.ok) {
    const message = typeof result?.error === 'string' ? result.error : `Central request failed with HTTP ${response.status}.`;
    throw new Error(message);
  }
  return result as T;
}

function query(values: Record<string, string | undefined>): string {
  const params = new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => value && params.set(key, value));
  const result = params.toString();
  return result ? `?${result}` : '';
}

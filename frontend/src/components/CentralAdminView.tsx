import React from 'react';
import { BarChart3, Building2, History, KeyRound, Play, RefreshCw, Save, ShieldCheck, Users } from 'lucide-react';
import {
  getCentralAudit,
  getCentralCatalog,
  getCentralDashboard,
  getCentralHistory,
  getCentralRuleProfiles,
  exportCentralReport,
  runCentralAnalysis,
  saveCentralProject,
  saveCentralRuleProfile,
  saveCentralTeam,
  saveCentralTenant,
  saveCentralUser,
  type CentralAuditEvent,
  type CentralCatalog,
  type CentralDashboard,
  type CentralAnalysis,
  type CentralRole,
} from '../services/centralService';

type Translate = (key: string, values?: Record<string, unknown>) => string;
type CentralSection = 'dashboard' | 'tenants' | 'users' | 'teams' | 'projects' | 'profiles' | 'history';

export function CentralAdminView({ enabled, authentication, license, locale = 'en', t, onBrandingChange }: { enabled: boolean; authentication: string; license?: { state: string; plan?: string; expiresAtUtc?: string; error?: string; allowsAccess: boolean }; locale?: 'tr' | 'en'; t: Translate; onBrandingChange?: (branding: { name: string; accentColor: string } | null) => void }) {
  const [token, setToken] = React.useState('');
  const [connected, setConnected] = React.useState(false);
  const [busy, setBusy] = React.useState(false);
  const [message, setMessage] = React.useState('');
  const [section, setSection] = React.useState<CentralSection>('dashboard');
  const [catalog, setCatalog] = React.useState<CentralCatalog | null>(null);
  const [dashboard, setDashboard] = React.useState<CentralDashboard | null>(null);
  const [history, setHistory] = React.useState<CentralAnalysis[]>([]);
  const [audit, setAudit] = React.useState<CentralAuditEvent[]>([]);
  const [profiles, setProfiles] = React.useState<Array<{ source: string; profile: Record<string, unknown> }>>([]);
  const [tenantId, setTenantId] = React.useState('');
  const [issuedApiKey, setIssuedApiKey] = React.useState('');

  React.useEffect(() => {
    const tenant = catalog?.tenants.find((item) => item.id === tenantId);
    if (!tenant) return;
    onBrandingChange?.({ name: tenant.brandingName || tenant.name, accentColor: tenant.brandingAccentColor || '#2563eb' });
  }, [catalog, onBrandingChange, tenantId]);

  const reload = React.useCallback(async (accessToken = token, requestedTenantId = tenantId) => {
    if (!accessToken.trim()) return;
    setBusy(true);
    setMessage('');
    try {
      const nextCatalog = await getCentralCatalog(accessToken);
      const selectedTenant = requestedTenantId || nextCatalog.tenants[0]?.id || nextCatalog.users[0]?.tenantId || '';
      setCatalog(nextCatalog);
      setTenantId(selectedTenant);
      const [nextDashboard, nextHistory, nextProfiles] = await Promise.all([
        getCentralDashboard(accessToken, selectedTenant || undefined),
        getCentralHistory(accessToken, selectedTenant || undefined),
        getCentralRuleProfiles(accessToken, selectedTenant || undefined),
      ]);
      setDashboard(nextDashboard);
      setHistory(nextHistory.analyses);
      setProfiles(nextProfiles.profiles);
      try {
        const nextAudit = await getCentralAudit(accessToken, selectedTenant || undefined);
        setAudit(nextAudit.events);
      } catch {
        setAudit([]);
      }
      setConnected(true);
    } catch (error) {
      setConnected(false);
      setMessage(error instanceof Error ? error.message : t('centralConnectionFailed'));
    } finally {
      setBusy(false);
    }
  }, [tenantId, t, token]);

  if (!enabled) {
    return <section className="central-empty"><ShieldCheck size={28} /><h2>{t('centralModeDisabled')}</h2><p>{t('centralModeDisabledHelp')}</p></section>;
  }

  if (!connected) {
    return (
      <section className="central-login">
        <KeyRound size={30} />
        <div><span className="eyebrow">{authentication}</span><h2>{t('centralWorkspace')}</h2><p>{t('centralLoginHelp')}</p></div>
        {license && license.state !== 'NotRequired' && <div className={license.allowsAccess ? 'central-license good' : 'central-license error-text'}><strong>{t('licenseStatus')}: {license.state}</strong><span>{license.plan ?? ''}{license.expiresAtUtc ? ` · ${t('expiresAt')} ${new Date(license.expiresAtUtc).toLocaleDateString()}` : ''}</span>{license.error && <span>{license.error}</span>}</div>}
        <label>{t('centralAccessToken')}<input type="password" value={token} onChange={(event) => setToken(event.target.value)} autoComplete="off" /></label>
        <button className="primary-action" type="button" disabled={busy || !token.trim() || license?.allowsAccess === false} onClick={() => void reload(token)}>{busy ? t('loading') : t('connect')}</button>
        {message && <p className="error-text" role="alert">{message}</p>}
      </section>
    );
  }

  const tabs: Array<[CentralSection, string, React.ComponentType<{ size?: number }>]> = [
    ['dashboard', t('companyDashboard'), BarChart3], ['tenants', t('tenants'), Building2], ['users', t('usersAndRoles'), Users],
    ['teams', t('teams'), Users], ['projects', t('teamProjects'), Building2], ['profiles', t('tenantRuleProfiles'), ShieldCheck], ['history', t('centralHistory'), History],
  ];

  return (
    <section className="central-workspace">
      <div className="central-toolbar">
        <div><span className="eyebrow">{t('centralWorkspace')}</span><h2>{catalog?.tenants.find((item) => item.id === tenantId)?.brandingName || catalog?.tenants.find((item) => item.id === tenantId)?.name || tenantId}</h2></div>
        <div className="central-toolbar-actions">
          {catalog && catalog.tenants.length > 1 && <select aria-label={t('tenant')} value={tenantId} onChange={(event) => void reload(token, event.target.value)}>{catalog.tenants.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select>}
          <button type="button" disabled={busy} onClick={() => void reload()}><RefreshCw size={16} />{t('refresh')}</button>
        </div>
      </div>
      {message && <p className="central-message" role="status">{message}</p>}
      {issuedApiKey && <div className="central-secret" role="alert"><strong>{t('newApiKey')}</strong><code>{issuedApiKey}</code><span>{t('apiKeyShownOnce')}</span></div>}
      <nav className="central-tabs">{tabs.map(([id, label, Icon]) => <button key={id} type="button" className={section === id ? 'active' : ''} onClick={() => setSection(id)}><Icon size={15} />{label}</button>)}</nav>
      {section === 'dashboard' && <DashboardPanel dashboard={dashboard} catalog={catalog} t={t} onAnalyze={async (projectId) => { setBusy(true); try { await runCentralAnalysis(token, projectId); setMessage(t('analysisCompleted')); await reload(); } catch (error) { setMessage(error instanceof Error ? error.message : t('analysisFailed')); } finally { setBusy(false); } }} />}
      {section === 'tenants' && <TenantPanel catalog={catalog} t={t} onSave={async (data) => { await saveCentralTenant(token, data); await reload(); }} />}
      {section === 'users' && <UserPanel catalog={catalog} tenantId={tenantId} t={t} onSave={async (data) => { const result = await saveCentralUser(token, data); setIssuedApiKey(result.apiKey); await reload(); }} />}
      {section === 'teams' && <TeamPanel catalog={catalog} tenantId={tenantId} t={t} onSave={async (data) => { await saveCentralTeam(token, data); await reload(); }} />}
      {section === 'projects' && <ProjectPanel catalog={catalog} tenantId={tenantId} busy={busy} t={t} onSave={async (data) => { await saveCentralProject(token, data); await reload(); }} onAnalyze={async (id) => { await runCentralAnalysis(token, id); await reload(); }} onExport={(id) => exportCentralReport(token, id, 'html', locale)} />}
      {section === 'profiles' && <ProfilePanel profiles={profiles} t={t} onSave={async (profile) => { await saveCentralRuleProfile(token, profile, tenantId || undefined); await reload(); }} />}
      {section === 'history' && <HistoryPanel history={history} audit={audit} catalog={catalog} t={t} />}
    </section>
  );
}

function DashboardPanel({ dashboard, catalog, t, onAnalyze }: { dashboard: CentralDashboard | null; catalog: CentralCatalog | null; t: Translate; onAnalyze: (id: string) => Promise<void> }) {
  if (!dashboard) return <p>{t('noData')}</p>;
  return <div className="central-panel">
    <div className="central-metrics"><Metric label={t('projects')} value={dashboard.projectCount} /><Metric label={t('analyzedProjects')} value={dashboard.analyzedProjectCount} /><Metric label={t('averageScore')} value={dashboard.averageScore ?? '-'} /><Metric label={t('monthlyUsage')} value={`${dashboard.monthlyUsage} / ${dashboard.monthlyQuota ?? '-'}`} /></div>
    <h3>{t('qualityTrends')}</h3>
    <div className="central-table"><div className="central-table-head"><span>{t('project')}</span><span>{t('team')}</span><span>{t('score')}</span><span>{t('findingsNav')}</span><span>{t('actions')}</span></div>{dashboard.projects.map((project) => <div className="central-table-row" key={project.id}><strong>{project.name}</strong><span>{catalog?.teams.find((team) => team.id === project.teamId)?.name ?? '-'}</span><span>{project.latest?.score ?? '-'}/{project.latest?.grade ?? '-'}</span><span>{project.latest?.findingCount ?? '-'}</span><button type="button" onClick={() => void onAnalyze(project.id)}><Play size={14} />{t('analyzeProject')}</button></div>)}</div>
  </div>;
}

function TenantPanel({ catalog, t, onSave }: { catalog: CentralCatalog | null; t: Translate; onSave: (data: { id: string; name: string; active: boolean; plan: string; monthlyAnalysisQuota: number; brandingName?: string; brandingAccentColor?: string }) => Promise<void> }) {
  const [form, setForm] = React.useState({ id: '', name: '', active: true, plan: 'Internal', monthlyAnalysisQuota: 1000, brandingName: '', brandingAccentColor: '#2563eb' });
  return <EntityPanel title={t('tenants')} items={catalog?.tenants.map((item) => `${item.name} · ${item.plan} · ${item.monthlyAnalysisQuota}`) ?? []}><FormGrid><TextField label="ID" value={form.id} onChange={(id) => setForm({ ...form, id })} /><TextField label={t('name')} value={form.name} onChange={(name) => setForm({ ...form, name })} /><label>{t('plan')}<select value={form.plan} onChange={(event) => setForm({ ...form, plan: event.target.value })}>{['Internal','Trial','Team','Enterprise'].map((plan) => <option key={plan}>{plan}</option>)}</select></label><TextField label={t('brandingName')} value={form.brandingName} onChange={(brandingName) => setForm({ ...form, brandingName })} /><TextField label={t('accentColor')} value={form.brandingAccentColor} onChange={(brandingAccentColor) => setForm({ ...form, brandingAccentColor })} /><TextField label={t('monthlyQuota')} type="number" value={String(form.monthlyAnalysisQuota)} onChange={(value) => setForm({ ...form, monthlyAnalysisQuota: Number(value) })} /><BooleanField label={t('active')} checked={form.active} onChange={(active) => setForm({ ...form, active })} /><SaveButton t={t} disabled={!form.id || !form.name} onClick={() => onSave(form)} /></FormGrid></EntityPanel>;
}

function UserPanel({ catalog, tenantId, t, onSave }: { catalog: CentralCatalog | null; tenantId: string; t: Translate; onSave: (data: { id: string; tenantId: string; email: string; displayName: string; role: CentralRole; active: boolean; teamIds: string[] }) => Promise<void> }) {
  const [form, setForm] = React.useState<{ id: string; email: string; displayName: string; role: CentralRole; active: boolean; teamIds: string[] }>({ id: '', email: '', displayName: '', role: 'Developer', active: true, teamIds: [] });
  return <EntityPanel title={t('usersAndRoles')} items={catalog?.users.map((item) => `${item.displayName} · ${item.email} · ${item.role}${item.active ? '' : ` · ${t('disabled')}`}`) ?? []}><FormGrid><TextField label="ID" value={form.id} onChange={(id) => setForm({ ...form, id })} /><TextField label={t('email')} value={form.email} onChange={(email) => setForm({ ...form, email })} /><TextField label={t('displayName')} value={form.displayName} onChange={(displayName) => setForm({ ...form, displayName })} /><label>{t('role')}<select value={form.role} onChange={(event) => setForm({ ...form, role: event.target.value as CentralRole })}>{['Viewer','Developer','Manager','TenantAdmin','SystemAdmin'].map((role) => <option key={role}>{role}</option>)}</select></label><BooleanField label={t('active')} checked={form.active} onChange={(active) => setForm({ ...form, active })} /><MultiSelect label={t('teams')} options={(catalog?.teams ?? []).map((team) => ({ id: team.id, label: team.name }))} selected={form.teamIds} onChange={(teamIds) => setForm({ ...form, teamIds })} /><SaveButton t={t} disabled={!form.id || !form.email || !form.displayName || !tenantId} onClick={() => onSave({ ...form, tenantId })} /></FormGrid></EntityPanel>;
}

function TeamPanel({ catalog, tenantId, t, onSave }: { catalog: CentralCatalog | null; tenantId: string; t: Translate; onSave: (data: { id: string; tenantId: string; name: string; memberUserIds: string[] }) => Promise<void> }) {
  const [id, setId] = React.useState(''); const [name, setName] = React.useState(''); const [memberUserIds, setMemberUserIds] = React.useState<string[]>([]);
  return <EntityPanel title={t('teams')} items={catalog?.teams.map((item) => `${item.name} · ${item.memberUserIds.length} ${t('members')}`) ?? []}><FormGrid><TextField label="ID" value={id} onChange={setId} /><TextField label={t('name')} value={name} onChange={setName} /><MultiSelect label={t('members')} options={(catalog?.users ?? []).map((user) => ({ id: user.id, label: user.displayName }))} selected={memberUserIds} onChange={setMemberUserIds} /><SaveButton t={t} disabled={!id || !name || !tenantId} onClick={() => onSave({ id, tenantId, name, memberUserIds })} /></FormGrid></EntityPanel>;
}

function ProjectPanel({ catalog, tenantId, busy, t, onSave, onAnalyze, onExport }: { catalog: CentralCatalog | null; tenantId: string; busy: boolean; t: Translate; onSave: (data: { id: string; tenantId: string; name: string; projectPath: string; ruleProfileId: string; teamId?: string; active: boolean }) => Promise<void>; onAnalyze: (id: string) => Promise<void>; onExport: (id: string) => Promise<void> }) {
  const [form, setForm] = React.useState({ id: '', name: '', projectPath: '', ruleProfileId: 'default', teamId: '', active: true });
  return <EntityPanel title={t('teamProjects')} items={[]}><FormGrid><TextField label="ID" value={form.id} onChange={(id) => setForm({ ...form, id })} /><TextField label={t('name')} value={form.name} onChange={(name) => setForm({ ...form, name })} /><TextField label={t('projectPath')} value={form.projectPath} onChange={(projectPath) => setForm({ ...form, projectPath })} /><TextField label={t('analysisProfile')} value={form.ruleProfileId} onChange={(ruleProfileId) => setForm({ ...form, ruleProfileId })} /><label>{t('team')}<select value={form.teamId} onChange={(event) => setForm({ ...form, teamId: event.target.value })}><option value="">{t('unassigned')}</option>{catalog?.teams.map((team) => <option key={team.id} value={team.id}>{team.name}</option>)}</select></label><BooleanField label={t('active')} checked={form.active} onChange={(active) => setForm({ ...form, active })} /><SaveButton t={t} disabled={!form.id || !form.name || !form.projectPath || !tenantId} onClick={() => onSave({ ...form, teamId: form.teamId || undefined, tenantId })} /></FormGrid><div className="central-entity-list">{catalog?.projects.map((item) => <div key={item.id}><span><strong>{item.name}</strong><small>{item.projectPath}</small></span><span className="central-row-actions"><button type="button" disabled={busy || !item.active} onClick={() => void onAnalyze(item.id)}><Play size={14} />{t('analyzeProject')}</button><button type="button" disabled={busy || !item.active} onClick={() => void onExport(item.id)}>{t('exportHtml')}</button></span></div>)}</div></EntityPanel>;
}

function ProfilePanel({ profiles, t, onSave }: { profiles: Array<{ source: string; profile: Record<string, unknown> }>; t: Translate; onSave: (profile: Record<string, unknown>) => Promise<void> }) {
  const [text, setText] = React.useState('');
  return <EntityPanel title={t('tenantRuleProfiles')} items={profiles.map((item) => `${String(item.profile.name ?? item.profile.id)} · ${item.source}`)}><p className="hint">{t('profileJsonHelp')}</p><textarea className="central-json" value={text} onChange={(event) => setText(event.target.value)} placeholder='{"id":"company-standard","name":"Company Standard","rules":[]}' /><SaveButton t={t} disabled={!text.trim()} onClick={() => onSave(JSON.parse(text) as Record<string, unknown>)} /></EntityPanel>;
}

function HistoryPanel({ history, audit, catalog, t }: { history: CentralAnalysis[]; audit: CentralAuditEvent[]; catalog: CentralCatalog | null; t: Translate }) {
  return <div className="central-history-grid"><EntityPanel title={t('centralHistory')} items={history.map((item) => `${catalog?.projects.find((project) => project.id === item.projectId)?.name ?? item.projectId} · ${item.status} · ${item.score ?? '-'}/${item.grade ?? '-'}`)} /><EntityPanel title={t('auditLog')} items={audit.map((item) => `${new Date(item.timestampUtc).toLocaleString()} · ${item.action} · ${item.resourceId}`)} /></div>;
}

function EntityPanel({ title, items, children }: React.PropsWithChildren<{ title: string; items: string[] }>) { return <section className="central-panel"><h3>{title}</h3>{children}<div className="central-entity-list">{items.map((item, index) => <div key={`${item}-${index}`}>{item}</div>)}</div></section>; }
function FormGrid({ children }: React.PropsWithChildren) { return <div className="central-form-grid">{children}</div>; }
function TextField({ label, value, onChange, type = 'text' }: { label: string; value: string; onChange: (value: string) => void; type?: string }) { return <label>{label}<input type={type} value={value} onChange={(event) => onChange(event.target.value)} /></label>; }
function BooleanField({ label, checked, onChange }: { label: string; checked: boolean; onChange: (checked: boolean) => void }) { return <label className="central-check"><input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />{label}</label>; }
function MultiSelect({ label, options, selected, onChange }: { label: string; options: Array<{ id: string; label: string }>; selected: string[]; onChange: (selected: string[]) => void }) { return <fieldset className="central-multi"><legend>{label}</legend>{options.length === 0 ? <span>-</span> : options.map((option) => <label key={option.id}><input type="checkbox" checked={selected.includes(option.id)} onChange={(event) => onChange(event.target.checked ? [...selected, option.id] : selected.filter((id) => id !== option.id))} />{option.label}</label>)}</fieldset>; }
function SaveButton({ t, disabled, onClick }: { t: Translate; disabled: boolean; onClick: () => Promise<void> }) { const [busy, setBusy] = React.useState(false); return <button className="primary-action" type="button" disabled={disabled || busy} onClick={async () => { setBusy(true); try { await onClick(); } finally { setBusy(false); } }}><Save size={15} />{busy ? t('saving') : t('save')}</button>; }
function Metric({ label, value }: { label: string; value: React.ReactNode }) { return <div className="central-metric"><span>{label}</span><strong>{value}</strong></div>; }

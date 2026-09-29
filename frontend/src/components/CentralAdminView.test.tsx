import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { vi } from 'vitest';
import { CentralAdminView } from './CentralAdminView';

vi.mock('../services/centralService', () => ({
  getCentralCatalog: vi.fn(async () => ({ tenants: [{ id: 'tenant-a', name: 'Acme', active: true, plan: 'Team', monthlyAnalysisQuota: 50 }], users: [], teams: [], projects: [], analyses: [], ruleProfiles: [] })),
  getCentralDashboard: vi.fn(async () => ({ tenantId: 'tenant-a', projectCount: 0, analyzedProjectCount: 0, averageScore: null, monthlyUsage: 0, monthlyQuota: 50, projects: [], teams: [], developers: [] })),
  getCentralHistory: vi.fn(async () => ({ analyses: [] })),
  getCentralAudit: vi.fn(async () => ({ events: [] })),
  getCentralRuleProfiles: vi.fn(async () => ({ tenantId: 'tenant-a', profiles: [] })),
  saveCentralTenant: vi.fn(), saveCentralUser: vi.fn(), saveCentralTeam: vi.fn(), saveCentralProject: vi.fn(), saveCentralRuleProfile: vi.fn(), runCentralAnalysis: vi.fn(),
}));

const t = (key: string) => ({ centralModeDisabled: 'Central mode is disabled', centralModeDisabledHelp: 'Enable it', centralWorkspace: 'Company Workspace', centralLoginHelp: 'Session only', centralAccessToken: 'Access token', connect: 'Connect', loading: 'Loading', companyDashboard: 'Company Dashboard', tenants: 'Tenants', usersAndRoles: 'Users & Roles', teams: 'Teams', teamProjects: 'Team Projects', tenantRuleProfiles: 'Rule Profiles', centralHistory: 'History', auditLog: 'Audit Log', analyzedProjects: 'Analyzed Projects', averageScore: 'Average Score', monthlyUsage: 'Monthly Usage', projects: 'Projects', qualityTrends: 'Quality Trends', refresh: 'Refresh' }[key] ?? key);

describe('CentralAdminView', () => {
  it('shows a safe disabled state when central mode is off', () => {
    render(<CentralAdminView enabled={false} authentication="None" t={t} />);
    expect(screen.getByText('Central mode is disabled')).toBeInTheDocument();
  });

  it('keeps the credential in component state and loads the tenant dashboard', async () => {
    render(<CentralAdminView enabled authentication="ApiKey" t={t} />);
    fireEvent.change(screen.getByLabelText('Access token'), { target: { value: 'session-key' } });
    fireEvent.click(screen.getByRole('button', { name: 'Connect' }));
    expect(await screen.findByText('Acme')).toBeInTheDocument();
    expect(screen.getByText('0 / 50')).toBeInTheDocument();
  });
});

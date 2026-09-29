import { vi } from 'vitest';
import { getCentralCatalog, getCentralStatus, runCentralAnalysis } from './centralService';
import { resetApiClientCacheForTests } from './apiClient';

describe('centralService', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    resetApiClientCacheForTests();
  });

  it('treats an unavailable central route as disabled', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => ({ status: 404, ok: false })));
    await expect(getCentralStatus()).resolves.toEqual({ enabled: false, authentication: 'None' });
  });

  it('sends the access token only in the authorization header', async () => {
    const fetchMock = vi.fn(async () => ({ ok: true, json: async () => ({ tenants: [], users: [], teams: [], projects: [], analyses: [], ruleProfiles: [] }) }));
    vi.stubGlobal('fetch', fetchMock);

    await getCentralCatalog('secret-token');

    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/central/catalog', expect.objectContaining({
      headers: expect.objectContaining({ Authorization: 'Bearer secret-token' }),
    }));
    expect(JSON.stringify(fetchMock.mock.calls)).not.toContain('localStorage');
  });

  it('runs only a registered project by identifier', async () => {
    const fetchMock = vi.fn(async () => ({ ok: true, json: async () => ({ analysisId: 'analysis-1' }) }));
    vi.stubGlobal('fetch', fetchMock);

    await runCentralAnalysis('token', 'project/a');

    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/central/projects/project%2Fa/analyze', expect.objectContaining({ method: 'POST' }));
  });
});

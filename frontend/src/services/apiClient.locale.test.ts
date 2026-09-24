import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  analyzeProject,
  analyzeProcessPdd,
  exportCustomRules,
  exportRuleProfiles,
  getApiLocale,
  getRuleProfiles,
  getRule,
  getRules,
  importCustomRules,
  saveCustomRule,
  saveRuleProfile,
  setApiLocale,
  testCustomRule,
} from './apiClient';

vi.mock('@tauri-apps/api/core', () => ({
  invoke: vi.fn(async () => 'http://127.0.0.1:5000'),
}));

describe('api locale', () => {
  afterEach(() => {
    setApiLocale('en');
    vi.unstubAllGlobals();
  });

  it('persists the selected API locale in the client service', () => {
    setApiLocale('tr');

    expect(getApiLocale()).toBe('tr');
  });

  it('adds the selected locale to analyze requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ projectName: 'Project' }),
    }));
    vi.stubGlobal('fetch', fetchMock);
    setApiLocale('tr');

    await analyzeProject('/tmp/project', 'default');

    const request = findRequest(fetchMock, '/api/uipath/projects/analyze');
    expect(JSON.parse(String(request.body))).toMatchObject({ projectPath: '/tmp/project', profileId: 'default', locale: 'tr' });
  });

  it('adds the selected locale to Process and PDD analysis requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ projectName: 'Project', pddFileName: 'PDD.md' }),
    }));
    vi.stubGlobal('fetch', fetchMock);
    setApiLocale('tr');

    await analyzeProcessPdd({ projectPath: '/tmp/project', pddFileName: 'PDD.md', pddContent: '# İş Kuralları' });

    const request = findRequest(fetchMock, '/api/uipath/projects/process-pdd-analysis');
    expect(JSON.parse(String(request.body))).toMatchObject({ projectPath: '/tmp/project', pddFileName: 'PDD.md', locale: 'tr' });
  });

  it('adds the selected locale to rule catalog requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => [],
    }));
    vi.stubGlobal('fetch', fetchMock);
    setApiLocale('tr');

    await getRules();
    await getRule('RPA007');

    expect(fetchMock).toHaveBeenNthCalledWith(1, 'http://127.0.0.1:5000/api/uipath/rules?locale=tr');
    expect(fetchMock).toHaveBeenNthCalledWith(2, 'http://127.0.0.1:5000/api/uipath/rules/RPA007?locale=tr');
  });

  it('posts custom rule test requests with project context', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ estimatedFindings: 2 }),
    }));
    vi.stubGlobal('fetch', fetchMock);
    const rule = { id: 'CUSTOM-001', name: 'Large workflow' };

    await testCustomRule('/tmp/uipath', rule);

    const request = (fetchMock as unknown as { mock: { calls: Array<[string, RequestInit]> } }).mock.calls[0][1];
    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/custom-rules/test', expect.any(Object));
    expect(JSON.parse(String(request.body))).toMatchObject({ projectPath: '/tmp/uipath', rule });
  });

  it('saves imports and exports custom rules through the API client', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ ok: true }),
    }));
    vi.stubGlobal('fetch', fetchMock);
    const rule = { id: 'CUSTOM-002', name: 'No delays' };

    await saveCustomRule(rule);
    await exportCustomRules();
    await importCustomRules([rule], true);

    const saveRequest = (fetchMock as unknown as { mock: { calls: Array<[string, RequestInit]> } }).mock.calls[0][1];
    const importRequest = (fetchMock as unknown as { mock: { calls: Array<[string, RequestInit]> } }).mock.calls[2][1];
    expect(JSON.parse(String(saveRequest.body))).toMatchObject(rule);
    expect(fetchMock).toHaveBeenNthCalledWith(2, 'http://127.0.0.1:5000/api/uipath/custom-rules/export');
    expect(JSON.parse(String(importRequest.body))).toMatchObject({ rules: [rule], overwrite: true });
  });

  it('loads saves and exports rule profiles through the API client', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ ok: true }),
    }));
    vi.stubGlobal('fetch', fetchMock);
    const profile = { id: 'company-standard', name: 'Company Standard', rules: [] };

    await getRuleProfiles();
    await saveRuleProfile(profile);
    await exportRuleProfiles();

    const saveRequest = (fetchMock as unknown as { mock: { calls: Array<[string, RequestInit]> } }).mock.calls[1][1];
    expect(fetchMock).toHaveBeenNthCalledWith(1, 'http://127.0.0.1:5000/api/uipath/rule-profiles');
    expect(JSON.parse(String(saveRequest.body))).toMatchObject(profile);
    expect(fetchMock).toHaveBeenNthCalledWith(3, 'http://127.0.0.1:5000/api/uipath/rule-profiles/export');
  });
});

function findRequest(fetchMock: ReturnType<typeof vi.fn>, path: string): RequestInit {
  const call = (fetchMock as unknown as { mock: { calls: Array<[string, RequestInit]> } }).mock.calls
    .find(([url]) => String(url).includes(path));
  if (!call) {
    throw new Error(`Expected request to ${path}.`);
  }

  return call[1];
}

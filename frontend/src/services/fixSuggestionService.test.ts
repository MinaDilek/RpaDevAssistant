import { vi } from 'vitest';
import { applyFix, getFixSuggestion, listBackups, setApiLocale, undoFix } from './apiClient';

vi.mock('@tauri-apps/api/core', () => ({
  invoke: vi.fn(async () => 'http://127.0.0.1:5000'),
}));

describe('getFixSuggestion', () => {
  afterEach(() => {
    setApiLocale('en');
    vi.unstubAllGlobals();
  });

  it('posts fix suggestion requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ suggestion: { title: 'Fix' } }),
    }));
    vi.stubGlobal('fetch', fetchMock);

    await getFixSuggestion({
      projectPath: '/tmp/project',
      ruleId: 'RPA007',
      workflowPath: 'Main.xaml',
      activityId: 'a1',
    });

    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/projects/fix-suggestions', expect.objectContaining({
      method: 'POST',
    }));
    const request = (fetchMock as unknown as { mock: { calls: Array<[string, RequestInit]> } }).mock.calls[0][1];
    expect(JSON.parse(String(request.body))).toMatchObject({
      ruleId: 'RPA007',
      activityId: 'a1',
    });
  });

  it('adds the selected locale to fix suggestion requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ suggestion: { title: 'Düzelt' } }),
    }));
    vi.stubGlobal('fetch', fetchMock);
    setApiLocale('tr');

    await getFixSuggestion({ projectPath: '/tmp/project', ruleId: 'RPA007' });

    const request = (fetchMock as unknown as { mock: { calls: Array<[string, RequestInit]> } }).mock.calls[0][1];
    expect(JSON.parse(String(request.body))).toMatchObject({ locale: 'tr' });
  });

  it('surfaces fix suggestion API errors', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: false,
      status: 400,
      json: async () => ({ error: 'ruleId is required.' }),
    })));

    await expect(getFixSuggestion({ projectPath: '/tmp/project', ruleId: '' })).rejects.toThrow('ruleId is required');
  });

  it('posts apply fix requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ success: true, applied: true }),
    }));
    vi.stubGlobal('fetch', fetchMock);

    await applyFix({
      projectPath: '/tmp/project',
      fixSuggestionId: 'fix1',
      ruleId: 'RPA007',
      workflowPath: 'Main.xaml',
      activityId: 'a1',
      propertyName: 'DisplayName',
      expectedCurrentValue: 'Click',
      suggestedValue: 'Click Login',
      expectedFileHash: 'abc123',
    });

    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/projects/fixes/apply', expect.objectContaining({
      method: 'POST',
    }));
    const request = (fetchMock as unknown as { mock: { calls: Array<[string, RequestInit]> } }).mock.calls[0][1];
    expect(JSON.parse(String(request.body))).toMatchObject({
      ruleId: 'RPA007',
      propertyName: 'DisplayName',
      expectedFileHash: 'abc123',
    });
  });

  it('surfaces apply fix API errors', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: false,
      status: 400,
      json: async () => ({ message: 'Fix is stale because the activity has changed since the suggestion was generated.' }),
    })));

    await expect(applyFix({
      projectPath: '/tmp/project',
      fixSuggestionId: 'fix1',
      ruleId: 'RPA007',
      workflowPath: 'Main.xaml',
      propertyName: 'DisplayName',
      expectedCurrentValue: 'Click',
      suggestedValue: 'Click Login',
    })).rejects.toThrow('Fix is stale');
  });

  it('loads backup history', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ backups: [{ backupId: 'b1' }] }),
    }));
    vi.stubGlobal('fetch', fetchMock);

    await listBackups('/tmp/project');

    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/projects/backups?projectPath=%2Ftmp%2Fproject');
  });

  it('posts undo requests and surfaces errors', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ success: true, restored: true }),
    }));
    vi.stubGlobal('fetch', fetchMock);

    await undoFix({
      projectPath: '/tmp/project',
      backupId: 'b1',
      workflowPath: 'Main.xaml',
      expectedCurrentHash: 'hash',
    });

    const request = (fetchMock as unknown as { mock: { calls: Array<[string, RequestInit]> } }).mock.calls[0][1];
    expect(JSON.parse(String(request.body))).toMatchObject({
      backupId: 'b1',
      expectedCurrentHash: 'hash',
    });

    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: false,
      status: 400,
      json: async () => ({ message: 'The workflow has changed since this fix was applied.' }),
    })));

    await expect(undoFix({
      projectPath: '/tmp/project',
      backupId: 'b1',
      workflowPath: 'Main.xaml',
    })).rejects.toThrow('workflow has changed');
  });
});

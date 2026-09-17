import { vi } from 'vitest';
import { analyzeConfig, checkHealth, compareAnalysisSnapshots, generateConfigWorkbook, getBackendBaseUrl, listAnalysisHistory, previewConfigChanges, resetApiClientCacheForTests } from './apiClient';

vi.mock('@tauri-apps/api/core', () => ({
  invoke: vi.fn(async () => 'http://127.0.0.1:49152'),
}));

describe('apiClient', () => {
  afterEach(() => {
    Reflect.deleteProperty(window, '__TAURI_INTERNALS__');
    vi.unstubAllGlobals();
    resetApiClientCacheForTests();
  });

  it('uses browser fallback API base URL outside Tauri', async () => {
    await expect(getBackendBaseUrl()).resolves.toBe('http://127.0.0.1:5000');
  });

  it('returns false when health check never succeeds', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => {
      throw new Error('offline');
    }));

    await expect(checkHealth(2, 1)).resolves.toBe(false);
  });

  it('returns true when health endpoint responds ok', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true })));

    await expect(checkHealth(1, 1)).resolves.toBe(true);
  });

  it('discovers the backend on the development API port when the default port is offline', async () => {
    const fetchMock = vi.fn(async (url: string) => {
      if (url === 'http://127.0.0.1:5186/api/health') {
        return { ok: true };
      }

      throw new Error('offline');
    });
    vi.stubGlobal('fetch', fetchMock);

    await expect(getBackendBaseUrl()).resolves.toBe('http://127.0.0.1:5186');
  });

  it('loads analysis history for a project', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ snapshots: [] }),
    }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(listAnalysisHistory('/tmp/project')).resolves.toEqual({ snapshots: [] });
    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/projects/analysis-history?projectPath=%2Ftmp%2Fproject');
  });

  it('loads global analysis history when no project path is supplied', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ snapshots: [{ projectName: 'Project A' }, { projectName: 'Project B' }] }),
    }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(listAnalysisHistory()).resolves.toEqual({ snapshots: [{ projectName: 'Project A' }, { projectName: 'Project B' }] });
    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/projects/analysis-history');
  });

  it('posts analysis comparison requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ scoreDelta: 4 }),
    }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(compareAnalysisSnapshots({
      projectPath: '/tmp/project',
      baselineSnapshotId: 'snap-1',
      targetSnapshotId: 'snap-2',
    })).resolves.toEqual({ scoreDelta: 4 });
    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/projects/analysis-history/compare', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ projectPath: '/tmp/project', baselineSnapshotId: 'snap-1', targetSnapshotId: 'snap-2' }),
    }));
  });

  it('posts config analysis requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ configFound: true }),
    }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(analyzeConfig('/tmp/project')).resolves.toEqual({ configFound: true });
    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/projects/config/analyze', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ projectPath: '/tmp/project' }),
    }));
  });

  it('posts manually selected config workbook path', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ configFound: true }),
    }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(analyzeConfig('/tmp/project', '/tmp/project/Data/CustomConfig.xlsx')).resolves.toEqual({ configFound: true });
    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/projects/config/analyze', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ projectPath: '/tmp/project', configPath: '/tmp/project/Data/CustomConfig.xlsx' }),
    }));
  });

  it('does not hide a missing Config endpoint by retrying another backend port', async () => {
    const fetchMock = vi.fn(async (url: string) => {
      if (url === 'http://127.0.0.1:5000/api/health') {
        return { ok: true };
      }

      if (url === 'http://127.0.0.1:5000/api/uipath/projects/config/analyze') {
        return {
          ok: false,
          status: 404,
          json: async () => ({}),
        };
      }

      return { ok: true, json: async () => ({ configFound: true }) };
    });
    vi.stubGlobal('fetch', fetchMock);

    await expect(analyzeConfig('/tmp/project')).rejects.toThrow('Config analysis failed with HTTP 404.');
    expect(fetchMock).not.toHaveBeenCalledWith('http://127.0.0.1:5187/api/uipath/projects/config/analyze', expect.anything());
    expect(fetchMock).not.toHaveBeenCalledWith('http://127.0.0.1:5186/api/uipath/projects/config/analyze', expect.anything());
  });

  it('posts config preview requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ isValid: true }),
    }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(previewConfigChanges({
      projectPath: '/tmp/project',
      removeKeys: ['UnusedKey'],
      additions: [{ key: 'MissingKey', value: '' }],
    })).resolves.toEqual({ isValid: true });
    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/projects/config/preview', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ projectPath: '/tmp/project', removeKeys: ['UnusedKey'], additions: [{ key: 'MissingKey', value: '' }] }),
    }));
  });

  it('posts config generate requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ success: true, generated: true }),
    }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(generateConfigWorkbook({
      projectPath: '/tmp/project',
      outputPath: '/tmp/Config.Generated.xlsx',
      additions: [{ key: 'EndpointUrl', value: 'https://example.test' }],
    })).resolves.toEqual({ success: true, generated: true });
    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/projects/config/generate', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ projectPath: '/tmp/project', outputPath: '/tmp/Config.Generated.xlsx', additions: [{ key: 'EndpointUrl', value: 'https://example.test' }] }),
    }));
  });
});

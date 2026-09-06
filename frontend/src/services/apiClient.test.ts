import { vi } from 'vitest';
import { checkHealth, compareAnalysisSnapshots, getBackendBaseUrl, listAnalysisHistory, resetApiClientCacheForTests } from './apiClient';

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
});

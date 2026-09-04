import { vi } from 'vitest';
import { checkHealth, getBackendBaseUrl } from './apiClient';

vi.mock('@tauri-apps/api/core', () => ({
  invoke: vi.fn(async () => 'http://127.0.0.1:49152'),
}));

describe('apiClient', () => {
  afterEach(() => {
    Reflect.deleteProperty(window, '__TAURI_INTERNALS__');
    vi.unstubAllGlobals();
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
});

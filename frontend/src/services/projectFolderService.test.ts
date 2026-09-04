import { vi } from 'vitest';
import { selectProjectFolder } from './projectFolderService';

vi.mock('@tauri-apps/plugin-dialog', () => ({
  open: vi.fn(),
}));

describe('selectProjectFolder', () => {
  afterEach(() => {
    Reflect.deleteProperty(window, '__TAURI_INTERNALS__');
    vi.clearAllMocks();
  });

  it('falls back to null in browser mode', async () => {
    await expect(selectProjectFolder()).resolves.toBeNull();
  });
});

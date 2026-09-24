import { vi } from 'vitest';
import { checkForDesktopUpdate, installDesktopUpdate, resetDesktopUpdateForTests } from './updateService';

const check = vi.fn();
const relaunch = vi.fn();

vi.mock('@tauri-apps/plugin-updater', () => ({ check: (...args: unknown[]) => check(...args) }));
vi.mock('@tauri-apps/plugin-process', () => ({ relaunch: (...args: unknown[]) => relaunch(...args) }));
vi.mock('./environment', () => ({ isTauriDesktop: vi.fn(() => true) }));

describe('desktop update service', () => {
  beforeEach(() => {
    resetDesktopUpdateForTests();
    check.mockReset();
    relaunch.mockReset();
  });

  it('returns signed update metadata without installing automatically', async () => {
    const downloadAndInstall = vi.fn();
    check.mockResolvedValue({ version: '0.2.0', body: 'Reliability fixes', downloadAndInstall, close: vi.fn() });

    await expect(checkForDesktopUpdate()).resolves.toEqual(expect.objectContaining({
      supported: true,
      available: true,
      version: '0.2.0',
      notes: 'Reliability fixes',
    }));
    expect(downloadAndInstall).not.toHaveBeenCalled();
  });

  it('installs only after an explicit call and relaunches', async () => {
    const downloadAndInstall = vi.fn().mockResolvedValue(undefined);
    check.mockResolvedValue({ version: '0.2.0', body: '', downloadAndInstall, close: vi.fn() });
    await checkForDesktopUpdate();

    await installDesktopUpdate();

    expect(downloadAndInstall).toHaveBeenCalledOnce();
    expect(relaunch).toHaveBeenCalledOnce();
  });

  it('reports updater errors without treating them as an update', async () => {
    check.mockRejectedValue(new Error('update endpoint unavailable'));
    await expect(checkForDesktopUpdate()).resolves.toEqual(expect.objectContaining({
      supported: true,
      available: false,
      error: 'update endpoint unavailable',
    }));
  });
});

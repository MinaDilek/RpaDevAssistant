import { check, type Update } from '@tauri-apps/plugin-updater';
import { relaunch } from '@tauri-apps/plugin-process';
import { isTauriDesktop } from './environment';

export interface DesktopUpdateStatus {
  supported: boolean;
  checking: boolean;
  available: boolean;
  installing: boolean;
  version?: string;
  notes?: string;
  error?: string;
}

let availableUpdate: Update | null = null;

export async function checkForDesktopUpdate(): Promise<DesktopUpdateStatus> {
  if (!isTauriDesktop()) {
    return { supported: false, checking: false, available: false, installing: false };
  }

  try {
    availableUpdate?.close();
    availableUpdate = await check();
    return {
      supported: true,
      checking: false,
      available: availableUpdate !== null,
      installing: false,
      version: availableUpdate?.version,
      notes: availableUpdate?.body ?? undefined,
    };
  } catch (error) {
    availableUpdate = null;
    return {
      supported: true,
      checking: false,
      available: false,
      installing: false,
      error: error instanceof Error ? error.message : String(error),
    };
  }
}

export async function installDesktopUpdate(): Promise<void> {
  if (!availableUpdate) {
    throw new Error('No verified desktop update is available.');
  }

  await availableUpdate.downloadAndInstall();
  await relaunch();
}

export function resetDesktopUpdateForTests(): void {
  availableUpdate = null;
}

import { open } from '@tauri-apps/plugin-dialog';
import { isTauriDesktop } from './environment';

export { isTauriDesktop };

export async function selectProjectFolder(): Promise<string | null> {
  if (!isTauriDesktop()) {
    return null;
  }

  const selected = await open({
    directory: true,
    multiple: false,
    title: 'Select UiPath Project',
  });

  return typeof selected === 'string' ? selected : null;
}

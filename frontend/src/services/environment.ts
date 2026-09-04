export function isTauriDesktop(): boolean {
  return typeof window !== 'undefined' && Boolean('__TAURI_INTERNALS__' in window);
}

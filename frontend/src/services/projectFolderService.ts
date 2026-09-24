import { open, save } from '@tauri-apps/plugin-dialog';
import { invoke } from '@tauri-apps/api/core';
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

export async function selectXamlWorkflowFiles(): Promise<string[]> {
  if (!isTauriDesktop()) {
    return [];
  }

  const selected = await open({
    directory: false,
    multiple: true,
    title: 'Select UiPath XAML Workflow',
    filters: [{ name: 'UiPath XAML Workflow', extensions: ['xaml'] }],
  });

  if (Array.isArray(selected)) {
    return selected.filter((item): item is string => typeof item === 'string');
  }

  return typeof selected === 'string' ? [selected] : [];
}

export async function selectConvertedWorkflowSavePath(defaultPath?: string): Promise<string | null> {
  if (!isTauriDesktop()) {
    return null;
  }

  const selected = await save({
    title: 'Save Converted Workflow As',
    defaultPath,
    filters: [{ name: 'UiPath XAML Workflow', extensions: ['xaml'] }],
  });

  return typeof selected === 'string' ? selected : null;
}

export async function selectGeneratedConfigSavePath(defaultPath?: string): Promise<string | null> {
  if (!isTauriDesktop()) {
    return null;
  }

  const selected = await save({
    title: 'Save Config Workbook As',
    defaultPath,
    filters: [{ name: 'Excel Workbook', extensions: ['xlsx'] }],
  });

  return typeof selected === 'string' ? selected : null;
}

export async function selectConfigWorkbookFile(): Promise<string | null> {
  if (!isTauriDesktop()) {
    return null;
  }

  const selected = await open({
    directory: false,
    multiple: false,
    title: 'Select UiPath Config Workbook',
    filters: [{ name: 'Excel Workbook', extensions: ['xlsx'] }],
  });

  return typeof selected === 'string' ? selected : null;
}

export async function selectPddDocumentFile(): Promise<string | null> {
  if (!isTauriDesktop()) {
    return null;
  }

  const selected = await open({
    directory: false,
    multiple: false,
    title: 'Select Process PDD',
    filters: [{ name: 'Process document', extensions: ['txt', 'md'] }],
  });

  return typeof selected === 'string' ? selected : null;
}

export async function openWorkflowInStudio(projectPath: string, workflowPath: string): Promise<void> {
  if (!isTauriDesktop()) {
    throw new Error('Opening workflows in UiPath Studio is available in the desktop application.');
  }

  await invoke('open_workflow_in_studio', { projectPath, workflowPath });
}

export interface DesktopStartupContext {
  projectPath?: string | null;
  workflowPath?: string | null;
}

export async function getDesktopStartupContext(): Promise<DesktopStartupContext> {
  if (!isTauriDesktop()) return {};
  return invoke<DesktopStartupContext>('startup_context');
}

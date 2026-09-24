import { vi } from 'vitest';
import { getDesktopStartupContext, openWorkflowInStudio, selectProjectFolder } from './projectFolderService';

const invoke = vi.fn();

vi.mock('@tauri-apps/plugin-dialog', () => ({
  open: vi.fn(),
  save: vi.fn(),
}));

vi.mock('@tauri-apps/api/core', () => ({
  invoke: (...args: unknown[]) => invoke(...args),
}));

describe('selectProjectFolder', () => {
  afterEach(() => {
    Reflect.deleteProperty(window, '__TAURI_INTERNALS__');
    vi.clearAllMocks();
  });

  it('falls back to null in browser mode', async () => {
    await expect(selectProjectFolder()).resolves.toBeNull();
  });

  it('does not launch a workflow outside desktop mode', async () => {
    await expect(openWorkflowInStudio('/tmp/project', 'Main.xaml')).rejects.toThrow('desktop application');
    expect(invoke).not.toHaveBeenCalled();
  });

  it('asks the desktop shell to open a project-relative workflow', async () => {
    Object.defineProperty(window, '__TAURI_INTERNALS__', { value: {}, configurable: true });
    invoke.mockResolvedValue(undefined);

    await expect(openWorkflowInStudio('C:\\Rpa\\Project', 'Business\\Login.xaml')).resolves.toBeUndefined();
    expect(invoke).toHaveBeenCalledWith('open_workflow_in_studio', {
      projectPath: 'C:\\Rpa\\Project',
      workflowPath: 'Business\\Login.xaml',
    });
  });

  it('loads UiPath Studio startup context only in desktop mode', async () => {
    await expect(getDesktopStartupContext()).resolves.toEqual({});
    Object.defineProperty(window, '__TAURI_INTERNALS__', { value: {}, configurable: true });
    invoke.mockResolvedValue({ projectPath: 'C:\\Rpa\\Project', workflowPath: 'Main.xaml' });

    await expect(getDesktopStartupContext()).resolves.toEqual({
      projectPath: 'C:\\Rpa\\Project',
      workflowPath: 'Main.xaml',
    });
    expect(invoke).toHaveBeenCalledWith('startup_context');
  });
});

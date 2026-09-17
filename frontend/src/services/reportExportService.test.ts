import { vi } from 'vitest';
import { invoke } from '@tauri-apps/api/core';
import { save } from '@tauri-apps/plugin-dialog';
import { resetApiClientCacheForTests } from './apiClient';
import { createDefaultReportFileName, exportReport, sanitizeFileName } from './reportExportService';

vi.mock('@tauri-apps/plugin-dialog', () => ({
  save: vi.fn(),
}));

vi.mock('@tauri-apps/api/core', () => ({
  invoke: vi.fn(),
}));

describe('reportExportService', () => {
  beforeEach(() => {
    resetApiClientCacheForTests();
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: true,
      text: async () => '{"schemaVersion":"1.0"}',
      headers: {
        get: (name: string) => name.toLowerCase() === 'content-type' ? 'application/json; charset=utf-8' : null,
      },
    })));
    vi.stubGlobal('URL', {
      createObjectURL: vi.fn(() => 'blob:report'),
      revokeObjectURL: vi.fn(),
    });
  });

  afterEach(() => {
    Reflect.deleteProperty(window, '__TAURI_INTERNALS__');
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it('creates default report filename', () => {
    const fileName = createDefaultReportFileName('Invoice Bot', 'html', new Date(2026, 7, 29, 22, 45));

    expect(fileName).toBe('Invoice-Bot-RPA-Analysis-20260829-2245.html');
  });

  it('creates default PDF report filename', () => {
    const fileName = createDefaultReportFileName('Invoice Bot', 'pdf', new Date(2026, 7, 29, 22, 45));

    expect(fileName).toBe('Invoice-Bot-RPA-Analysis-20260829-2245.pdf');
  });

  it('sanitizes invalid filename characters', () => {
    expect(sanitizeFileName('Invoice:Bot*?')).toBe('Invoice-Bot');
  });

  it('uses browser download fallback outside Tauri', async () => {
    const appendSpy = vi.spyOn(document.body, 'append');
    const click = vi.fn();
    vi.spyOn(document, 'createElement').mockReturnValue({
      click,
      remove: vi.fn(),
      set href(value: string) {
        void value;
      },
      set download(value: string) {
        void value;
      },
    } as unknown as HTMLAnchorElement);

    await expect(exportReport({ projectPath: '/tmp/project', projectName: 'Project', format: 'json' })).resolves.toBe('JSON report downloaded.');
    expect(appendSpy).toHaveBeenCalled();
    expect(click).toHaveBeenCalled();
  });

  it('exports PDF through the desktop save dialog inside Tauri', async () => {
    Reflect.set(window, '__TAURI_INTERNALS__', {});
    vi.mocked(save).mockResolvedValue('/tmp/report.pdf');
    vi.mocked(invoke).mockImplementation(async (command) => {
      if (command === 'backend_base_url') {
        return 'http://127.0.0.1:5000';
      }

      return null;
    });

    await expect(exportReport({ projectPath: '/tmp/project', projectName: 'Project', format: 'pdf' })).resolves.toBe('PDF report exported successfully.');

    expect(fetch).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/projects/report', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ projectPath: '/tmp/project', profileId: 'default', format: 'pdf', locale: 'en' }),
    }));
    expect(save).toHaveBeenCalledWith(expect.objectContaining({
      filters: [{ name: 'PDF Report', extensions: ['pdf'] }],
    }));
    expect(invoke).toHaveBeenCalledWith('write_report_file', {
      path: '/tmp/report.pdf',
      contents: '{"schemaVersion":"1.0"}',
    });
  });
});

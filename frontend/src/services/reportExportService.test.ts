import { vi } from 'vitest';
import { createDefaultReportFileName, exportReport, sanitizeFileName } from './reportExportService';

vi.mock('@tauri-apps/plugin-dialog', () => ({
  save: vi.fn(),
}));

vi.mock('@tauri-apps/api/core', () => ({
  invoke: vi.fn(),
}));

describe('reportExportService', () => {
  beforeEach(() => {
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
});

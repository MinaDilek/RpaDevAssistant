import { save } from '@tauri-apps/plugin-dialog';
import { invoke } from '@tauri-apps/api/core';
import { getBackendBaseUrl } from './apiClient';
import { isTauriDesktop } from './environment';

export type ReportFormat = 'json' | 'html';

export interface ExportReportInput {
  projectPath: string;
  projectName?: string | null;
  profileId?: string;
  format: ReportFormat;
  locale?: 'tr' | 'en';
}

export async function exportReport(input: ExportReportInput): Promise<string> {
  const report = await fetchReport(input.projectPath, input.profileId ?? 'default', input.format, input.locale ?? 'en');
  const defaultFileName = createDefaultReportFileName(input.projectName, input.format);

  if (isTauriDesktop()) {
    const targetPath = await save({
      title: `Export ${input.format.toUpperCase()} Report`,
      defaultPath: defaultFileName,
      filters: [
        {
          name: input.format === 'html' ? 'HTML Report' : 'JSON Report',
          extensions: [input.format],
        },
      ],
    });

    if (!targetPath) {
      return 'Export cancelled.';
    }

    await invoke('write_report_file', {
      path: targetPath,
      contents: report.content,
    });
    return `${input.format.toUpperCase()} report exported successfully.`;
  }

  downloadInBrowser(report.content, report.contentType, report.fileName || defaultFileName);
  return `${input.format.toUpperCase()} report downloaded.`;
}

export function createDefaultReportFileName(projectName: string | null | undefined, format: ReportFormat, date = new Date()): string {
  const safeProjectName = sanitizeFileName(projectName || 'UiPathProject');
  const timestamp = [
    date.getFullYear(),
    pad(date.getMonth() + 1),
    pad(date.getDate()),
    '-',
    pad(date.getHours()),
    pad(date.getMinutes()),
  ].join('');
  return `${safeProjectName}-RPA-Analysis-${timestamp}.${format}`;
}

export function sanitizeFileName(value: string): string {
  const sanitized = value
    .trim()
    .replace(/[<>:"/\\|?*\u0000-\u001f]/g, '-')
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-')
    .replace(/^[.-]+|[.-]+$/g, '');

  return (sanitized || 'UiPathProject').slice(0, 80);
}

async function fetchReport(projectPath: string, profileId: string, format: ReportFormat, locale: 'tr' | 'en'): Promise<{ content: string; contentType: string; fileName?: string }> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/projects/report`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ projectPath, profileId, format, locale }),
  });

  if (!response.ok) {
    throw new Error(`Report export failed with HTTP ${response.status}.`);
  }

  const contentDisposition = response.headers.get('content-disposition');
  return {
    content: await response.text(),
    contentType: response.headers.get('content-type') ?? (format === 'html' ? 'text/html' : 'application/json'),
    fileName: parseFileName(contentDisposition),
  };
}

function downloadInBrowser(content: string, contentType: string, fileName: string): void {
  const blob = new Blob([content], { type: contentType });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

function parseFileName(contentDisposition: string | null): string | undefined {
  if (!contentDisposition) {
    return undefined;
  }

  const match = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(contentDisposition);
  return match ? decodeURIComponent(match[1]) : undefined;
}

function pad(value: number): string {
  return value.toString().padStart(2, '0');
}

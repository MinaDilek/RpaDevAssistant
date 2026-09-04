import { invoke } from '@tauri-apps/api/core';
import { isTauriDesktop } from './environment';

const DEFAULT_BROWSER_API_BASE_URL = 'http://127.0.0.1:5000';

let cachedDesktopBackendUrl: string | null = null;
let currentLocale: 'tr' | 'en' = 'en';

export function setApiLocale(locale: 'tr' | 'en'): void {
  currentLocale = locale;
}

export function getApiLocale(): 'tr' | 'en' {
  return currentLocale;
}

function withLocale<T extends Record<string, unknown>>(input: T): T & { locale: 'tr' | 'en' } {
  return { ...input, locale: currentLocale };
}

export async function getBackendBaseUrl(): Promise<string> {
  const configuredUrl = import.meta.env.VITE_API_BASE_URL;
  if (configuredUrl) {
    return trimTrailingSlash(configuredUrl);
  }

  if (isTauriDesktop()) {
    cachedDesktopBackendUrl ??= trimTrailingSlash(await invoke<string>('backend_base_url'));
    return cachedDesktopBackendUrl;
  }

  return DEFAULT_BROWSER_API_BASE_URL;
}

export async function checkHealth(maxAttempts = 20, delayMs = 250): Promise<boolean> {
  for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
    try {
      const baseUrl = await getBackendBaseUrl();
      const response = await fetch(`${baseUrl}/api/health`);
      if (response.ok) {
        return true;
      }
    } catch {
      // Retry while the desktop sidecar is starting.
    }

    await delay(delayMs);
  }

  return false;
}

export async function analyzeProject(projectPath: string, profileId = 'default'): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/projects/analyze`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(withLocale({ projectPath, profileId })),
  });

  if (!response.ok) {
    throw new Error(`Analysis request failed with HTTP ${response.status}.`);
  }

  return response.json();
}

export async function runAiReview(input: {
  projectPath: string;
  profileId?: string;
  scope: 'Project' | 'Workflow';
  workflowPath?: string;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/projects/ai-review`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(withLocale(input)),
  });

  const result = await response.json();
  if (!response.ok) {
    const message = typeof result?.errorMessage === 'string'
      ? result.errorMessage
      : typeof result?.error === 'string'
        ? result.error
        : `AI review request failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function askProject(input: {
  projectPath: string;
  profileId?: string;
  preferredWorkflowPath?: string;
  question: string;
  maxEvidenceItems?: number;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/projects/ask`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(withLocale(input)),
  });

  const result = await response.json();
  if (!response.ok) {
    const message = typeof result?.error === 'string'
      ? result.error
      : `Ask Project request failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function getFixSuggestion(input: {
  projectPath: string;
  profileId?: string;
  ruleId: string;
  workflowPath?: string;
  activityId?: string | null;
  propertyName?: string | null;
  useAi?: boolean;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/projects/fix-suggestions`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(withLocale(input)),
  });

  const result = await response.json();
  if (!response.ok) {
    const message = typeof result?.error === 'string'
      ? result.error
      : `Fix suggestion request failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function applyFix(input: {
  projectPath: string;
  fixSuggestionId: string;
  ruleId: string;
  workflowPath: string;
  activityId?: string | null;
  propertyName: string;
  expectedCurrentValue?: string | null;
  suggestedValue: string;
  expectedFileHash?: string | null;
  createBackup?: boolean;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/projects/fixes/apply`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(input),
  });

  const result = await response.json();
  if (!response.ok) {
    const message = typeof result?.message === 'string'
      ? result.message
      : typeof result?.error === 'string'
        ? result.error
        : `Apply fix request failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function listBackups(projectPath: string): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/projects/backups?projectPath=${encodeURIComponent(projectPath)}`);
  const result = await response.json();
  if (!response.ok) {
    const message = typeof result?.error === 'string'
      ? result.error
      : `Backup history request failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function undoFix(input: {
  projectPath: string;
  backupId: string;
  workflowPath: string;
  expectedCurrentHash?: string | null;
  createSafetyBackup?: boolean;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/projects/fixes/undo`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(input),
  });

  const result = await response.json();
  if (!response.ok) {
    const message = typeof result?.message === 'string'
      ? result.message
      : typeof result?.error === 'string'
        ? result.error
        : `Undo request failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function validateProject(projectPath: string): Promise<{ looksLikeUiPathProject: boolean; messages: string[] }> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/projects/validate`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ projectPath }),
  });

  if (!response.ok) {
    throw new Error(`Project validation failed with HTTP ${response.status}.`);
  }

  return response.json();
}

export async function getRules(): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/rules?locale=${encodeURIComponent(currentLocale)}`);
  if (!response.ok) {
    throw new Error(`Rule catalog request failed with HTTP ${response.status}.`);
  }

  return response.json();
}

export async function getRule(id: string): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/rules/${encodeURIComponent(id)}?locale=${encodeURIComponent(currentLocale)}`);
  if (!response.ok) {
    throw new Error(`Rule detail request failed with HTTP ${response.status}.`);
  }

  return response.json();
}

export async function getCustomRules(): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/custom-rules`);
  if (!response.ok) {
    throw new Error(`Custom rules request failed with HTTP ${response.status}.`);
  }

  return response.json();
}

export async function getRuleProfiles(): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/rule-profiles`);
  if (!response.ok) {
    throw new Error(`Rule profiles request failed with HTTP ${response.status}.`);
  }

  return response.json();
}

export async function saveRuleProfile(profile: unknown): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/rule-profiles`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(profile),
  });
  const result = await response.json();
  if (!response.ok) {
    const message = Array.isArray(result?.errors) ? result.errors.join(' ') : `Save rule profile failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function exportRuleProfiles(): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/rule-profiles/export`);
  if (!response.ok) {
    throw new Error(`Export rule profiles failed with HTTP ${response.status}.`);
  }

  return response.json();
}

export async function saveCustomRule(rule: unknown): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/custom-rules`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(rule),
  });
  const result = await response.json();
  if (!response.ok) {
    const message = Array.isArray(result?.errors) ? result.errors.join(' ') : `Save custom rule failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function testCustomRule(projectPath: string, rule: unknown): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/custom-rules/test`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ projectPath, rule }),
  });
  const result = await response.json();
  if (!response.ok) {
    const message = Array.isArray(result?.errors) ? result.errors.join(' ') : `Test custom rule failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function exportCustomRules(): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/custom-rules/export`);
  if (!response.ok) {
    throw new Error(`Export custom rules failed with HTTP ${response.status}.`);
  }

  return response.json();
}

export async function importCustomRules(rules: unknown[], overwrite = false): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/custom-rules/import`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ rules, overwrite }),
  });
  const result = await response.json();
  if (!response.ok) {
    const message = Array.isArray(result?.errors) ? result.errors.join(' ') : `Import custom rules failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

function trimTrailingSlash(value: string): string {
  return value.replace(/\/+$/, '');
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    window.setTimeout(resolve, ms);
  });
}

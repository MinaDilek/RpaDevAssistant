import { invoke } from '@tauri-apps/api/core';
import { isTauriDesktop } from './environment';

const DEFAULT_BROWSER_API_BASE_URL = 'http://127.0.0.1:5000';
const BROWSER_DEV_API_BASE_URLS = [
  DEFAULT_BROWSER_API_BASE_URL,
  'http://127.0.0.1:5186',
  'http://127.0.0.1:5187',
];

let cachedDesktopBackendUrl: string | null = null;
let cachedBrowserBackendUrl: string | null = null;
let currentLocale: 'tr' | 'en' = 'en';

export function setApiLocale(locale: 'tr' | 'en'): void {
  currentLocale = locale;
}

export function getApiLocale(): 'tr' | 'en' {
  return currentLocale;
}

export function resetApiClientCacheForTests(): void {
  cachedDesktopBackendUrl = null;
  cachedBrowserBackendUrl = null;
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

  const discoveredBrowserBackendUrl = cachedBrowserBackendUrl ?? await resolveBrowserBackendUrl();
  if (discoveredBrowserBackendUrl) {
    cachedBrowserBackendUrl = discoveredBrowserBackendUrl;
    return discoveredBrowserBackendUrl;
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

async function resolveBrowserBackendUrl(): Promise<string | null> {
  for (const baseUrl of BROWSER_DEV_API_BASE_URLS) {
    if (await canReachHealthEndpoint(baseUrl)) {
      return baseUrl;
    }
  }

  return null;
}

async function canReachHealthEndpoint(baseUrl: string): Promise<boolean> {
  try {
    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), 350);
    try {
      const response = await fetch(`${baseUrl}/api/health`, { signal: controller.signal });
      return response.ok;
    } finally {
      window.clearTimeout(timeout);
    }
  } catch {
    return false;
  }
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

export async function analyzeProcessPdd(input: {
  projectPath: string;
  pddPath?: string;
  pddFileName?: string;
  pddContent?: string;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/projects/process-pdd-analysis`, {
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
      : `Process and PDD analysis failed with HTTP ${response.status}.`;
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

export async function applyAllFixes(input: {
  projectPath: string;
  profileId?: string;
  maxFixes?: number;
  createBackup?: boolean;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/projects/fixes/apply-all`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(withLocale(input)),
  });
  const result = await response.json();
  if (!response.ok) {
    throw new Error(typeof result?.message === 'string'
      ? result.message
      : typeof result?.error === 'string'
        ? result.error
        : `Apply all fixes request failed with HTTP ${response.status}.`);
  }
  return result;
}

export async function renameWorkflow(input: {
  projectPath: string;
  workflowPath: string;
  newWorkflowPath: string;
  expectedFileHash?: string | null;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/projects/fixes/rename-workflow`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  });
  const result = await response.json();
  if (!response.ok) {
    throw new Error(typeof result?.message === 'string'
      ? result.message
      : typeof result?.error === 'string'
        ? result.error
        : `Workflow rename request failed with HTTP ${response.status}.`);
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

export async function listAnalysisHistory(projectPath?: string): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const query = projectPath ? `?projectPath=${encodeURIComponent(projectPath)}` : '';
  const response = await fetch(`${baseUrl}/api/uipath/projects/analysis-history${query}`);
  const result = await response.json();
  if (!response.ok) {
    const message = typeof result?.error === 'string'
      ? result.error
      : `Analysis history request failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function compareAnalysisSnapshots(input: {
  projectPath: string;
  baselineSnapshotId: string;
  targetSnapshotId: string;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/projects/analysis-history/compare`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(input),
  });

  const result = await response.json();
  if (!response.ok) {
    const message = typeof result?.error === 'string'
      ? result.error
      : `Analysis comparison request failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function compareGitRefs(input: {
  projectPath: string;
  baselineRef: string;
  targetRef: string;
  profileId?: string;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/git/compare`, {
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
        : `Git comparison request failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function getOrchestratorSummary(): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/orchestrator/summary`);
  const result = await response.json();
  if (!response.ok) {
    const message = typeof result?.message === 'string' ? result.message : `Orchestrator request failed with HTTP ${response.status}.`;
    throw new Error(message);
  }
  return result;
}

export async function reviewPullRequest(input: { projectPath: string; provider: string; repository: string; pullRequestId: number; profileId?: string }): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/source-control/pull-requests/review`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(input),
  });
  const result = await response.json();
  if (!response.ok) throw new Error(result?.message ?? `Pull request review failed with HTTP ${response.status}.`);
  return result;
}

export async function postPullRequestComment(input: { provider: string; repository: string; pullRequestId: number; body: string }): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/source-control/pull-requests/comment`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(input),
  });
  const result = await response.json();
  if (!response.ok) throw new Error(result?.message ?? `Pull request comment failed with HTTP ${response.status}.`);
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

export async function analyzeConfig(projectPath: string, configPath?: string | null): Promise<unknown> {
  return postJsonRequest('/api/uipath/projects/config/analyze', { projectPath, configPath }, 'Config analysis failed');
}

async function postJsonRequest(path: string, body: unknown, fallbackMessage: string): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await postJson(baseUrl, path, body);
  if (response.ok) {
    return response.json();
  }

  const result = await response.json().catch(() => ({}));
  const message = typeof result?.message === 'string'
    ? result.message
    : typeof result?.error === 'string'
      ? result.error
      : `${fallbackMessage} with HTTP ${response.status}.`;
  throw new Error(message);
}

function postJson(baseUrl: string, path: string, body: unknown): Promise<Response> {
  return fetch(`${baseUrl}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  });
}

export async function previewConfigChanges(input: {
  projectPath: string;
  configPath?: string | null;
  removeKeys?: string[];
  additions?: Array<{ key: string; value: string; description?: string | null; source?: string | null }>;
}): Promise<unknown> {
  return postJsonRequest('/api/uipath/projects/config/preview', input, 'Config preview failed');
}

export async function generateConfigWorkbook(input: {
  projectPath: string;
  configPath?: string | null;
  outputPath: string;
  removeKeys?: string[];
  additions?: Array<{ key: string; value: string; description?: string | null; source?: string | null }>;
}): Promise<unknown> {
  return postJsonRequest('/api/uipath/projects/config/generate', input, 'Config generation failed');
}

export async function analyzeFlowchartConversion(input: {
  projectPath: string;
  workflowPath: string;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/workflows/flowchart-conversion/analyze`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(input),
  });

  const result = await response.json();
  if (!response.ok) {
    const message = typeof result?.errors?.[0] === 'string'
      ? result.errors[0]
      : typeof result?.error === 'string'
        ? result.error
        : `Flowchart conversion analysis failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function applyFlowchartConversion(input: {
  projectPath: string;
  workflowPath: string;
  expectedWorkflowHash?: string | null;
  confirmed: boolean;
  createBackup?: boolean;
  replaceCustomActivitiesWithUiPathStandard?: boolean;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/workflows/flowchart-conversion/apply`, {
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
        : `Flowchart conversion apply failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function rollbackFlowchartConversion(input: {
  projectPath: string;
  workflowPath: string;
  backupId: string;
  expectedCurrentHash?: string | null;
  createSafetyBackup?: boolean;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/workflows/flowchart-conversion/rollback`, {
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
        : `Flowchart conversion rollback failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function analyzeStandaloneFlowchart(input: {
  xamlFilePath: string;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/workflows/flowchart-conversion/standalone/analyze`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(input),
  });

  const result = await response.json();
  if (!response.ok) {
    const message = typeof result?.errors?.[0] === 'string'
      ? result.errors[0]
      : typeof result?.error === 'string'
        ? result.error
        : `Standalone Flowchart analysis failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return result;
}

export async function convertStandaloneFlowchart(input: {
  xamlFilePath: string;
  outputPath: string;
  expectedWorkflowHash?: string | null;
  confirmed: boolean;
  replaceCustomActivitiesWithUiPathStandard?: boolean;
}): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/workflows/flowchart-conversion/standalone/convert`, {
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
        : `Standalone Flowchart conversion failed with HTTP ${response.status}.`;
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

export async function exportRuleModule(input: { moduleId: string; name: string; version: string; publisher?: string }): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const query = new URLSearchParams({ moduleId: input.moduleId, name: input.name, version: input.version });
  if (input.publisher) query.set('publisher', input.publisher);
  const response = await fetch(`${baseUrl}/api/uipath/rule-modules/export?${query}`);
  const result = await response.json();
  if (!response.ok) throw new Error(result?.error ?? `Export rule module failed with HTTP ${response.status}.`);
  return result;
}

export async function importRuleModule(module: unknown, overwrite = false): Promise<unknown> {
  const baseUrl = await getBackendBaseUrl();
  const response = await fetch(`${baseUrl}/api/uipath/rule-modules/import`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ module, overwrite }),
  });
  const result = await response.json();
  if (!response.ok) {
    const message = Array.isArray(result?.errors) ? result.errors.join(' ') : `Import rule module failed with HTTP ${response.status}.`;
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

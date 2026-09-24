import React from 'react';
import type { AnalysisResponse, CustomRuleDefinition, RuleCatalogItem, RuleProfile, getWorkflowHealth } from '../services/reportViewModel';
import type { Locale } from '../localization';

export type HealthState = 'checking' | 'ready' | 'unavailable';
export type ActiveTab = 'overview' | 'findings' | 'history' | 'workflows' | 'dependencies' | 'orchestrator' | 'flowchartConverter' | 'config' | 'report' | 'ask' | 'processPdd' | 'rules' | 'settings';

export function Metric({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="metric-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

export function safeGetStoredLocale(): string | null {
  try {
    return typeof window.localStorage?.getItem === 'function'
      ? window.localStorage.getItem('rpadevassistant.locale')
      : null;
  } catch {
    return null;
  }
}

export function safeStoreLocale(locale: Locale): void {
  try {
    if (typeof window.localStorage?.setItem === 'function') {
      window.localStorage.setItem('rpadevassistant.locale', locale);
    }
  } catch {
    // Persistence is optional in test and restricted browser contexts.
  }
}

export function readProjectProfileId(projectPath: string): string | null {
  const key = normalizeStoredProjectPath(projectPath);
  if (!key) {
    return null;
  }

  try {
    const raw = window.localStorage?.getItem('rpadevassistant.projectProfiles');
    if (!raw) {
      return null;
    }

    const stored = JSON.parse(raw) as Record<string, string>;
    return stored[key] ?? null;
  } catch {
    return null;
  }
}

export function saveProjectProfileId(projectPath: string, profileId: string): void {
  const key = normalizeStoredProjectPath(projectPath);
  if (!key || !profileId) {
    return;
  }

  try {
    const raw = window.localStorage?.getItem('rpadevassistant.projectProfiles');
    const stored = raw ? (JSON.parse(raw) as Record<string, string>) : {};
    stored[key] = profileId;
    window.localStorage?.setItem('rpadevassistant.projectProfiles', JSON.stringify(stored));
  } catch {
    // Per-project profile selection is a convenience; analysis still works without persistence.
  }
}

export function normalizeStoredProjectPath(projectPath: string): string {
  return projectPath.trim().replaceAll('\\', '/').replace(/\/+$/, '').toLowerCase();
}

export function downloadJson(fileName: string, value: unknown): void {
  const blob = new Blob([JSON.stringify(value, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

export function normalizeWorkflowPath(value: string): string {
  return value
    .trim()
    .replace(/^["']|["']$/g, '')
    .replaceAll('\\', '/')
    .replace(/^\.\//, '')
    .normalize('NFC')
    .toLowerCase();
}

export function normalizeCategoryValue(value: string | null | undefined): string {
  if (!value) {
    return '';
  }

  const normalized = value.replace(/\s+/g, '').toLowerCase();
  const match = ruleCategories().find((category) => category.toLowerCase() === normalized);
  return match ?? value;
}

export function localizeCategoryLabel(category: string, t: (key: string, values?: Record<string, unknown>) => string): string {
  const key = `category${category}`;
  const translated = t(key);
  return translated === key ? category : translated;
}

export function getWorkflowType(path: string): string {
  const normalized = path.replaceAll('\\', '/').toLowerCase();
  if (normalized.endsWith('/main.xaml') || normalized === 'main.xaml') {
    return 'Main';
  }

  if (normalized.includes('/framework/')) {
    return 'Framework';
  }

  if (normalized.includes('/business/')) {
    return 'Business';
  }

  if (normalized.includes('/utility/') || normalized.includes('/utilities/') || normalized.includes('/lib/')) {
    return 'Utility';
  }

  return 'Business';
}

export function formatWorkflowStructure(
  workflow: { structureType?: string; containsFlowchart?: boolean; flowchartCount?: number },
  t: (key: string, values?: Record<string, unknown>) => string,
): string {
  const structure = workflow.structureType ?? t('unknown');
  if (!workflow.containsFlowchart || structure === 'Flowchart') {
    return structure;
  }

  const count = workflow.flowchartCount && workflow.flowchartCount > 1 ? ` (${workflow.flowchartCount})` : '';
  return `${structure} + ${t('containsFlowchart')}${count}`;
}

export function estimateWorkflowScore(workflow: ReturnType<typeof getWorkflowHealth>[number]): number {
  const findingPenalty = workflow.findingCount * 7;
  const sizePenalty = workflow.activityCount > 100 ? 8 : 0;
  return Math.max(40, Math.min(100, 100 - findingPenalty - sizePenalty));
}

export function localizeComplexityLevel(level: string | null | undefined, t: (key: string, values?: Record<string, unknown>) => string): string {
  if (!level) {
    return t('unknown');
  }

  return level === 'VeryHigh' || level === 'Very High'
    ? t('veryHigh')
    : t(level.charAt(0).toLowerCase() + level.slice(1));
}

export function complexityBadgeClass(level: string | null | undefined): string {
  return level === 'High' || level === 'VeryHigh' || level === 'Very High'
    ? 'risk'
    : level === 'Medium'
      ? 'review'
      : 'good';
}

export function buildReviewSummary(analysis: AnalysisResponse, t: (key: string, values?: Record<string, unknown>) => string): string {
  const findings = analysis.analysis?.findings ?? [];
  if (findings.length === 0) {
    return t('noIssuesSummary');
  }

  const topCategories = Array.from(new Set(findings.map((finding) => finding.category).filter(Boolean))).slice(0, 3);
  return t('reviewSummaryText', { categories: topCategories.length ? topCategories.join(', ') : undefined });
}

export function createDefaultCustomRule(): CustomRuleDefinition {
  return {
    id: `CUSTOM-${Date.now().toString().slice(-6)}`,
    name: 'Large Nested Workflow',
    description: 'Workflow matches company-defined complexity criteria.',
    recommendation: 'Review the workflow and split it when appropriate.',
    category: 'Maintainability',
    severity: 'Warning',
    scope: 'Workflow',
    enabled: true,
    weight: 2,
    maxPenalty: 10,
    matchMode: 'All',
    conditions: [
      { field: 'Workflow.ExecutableActivityCount', operator: 'GreaterThanOrEqual', value: '100', caseSensitive: false },
      { field: 'Workflow.MaxNestingDepth', operator: 'GreaterThanOrEqual', value: '8', caseSensitive: false },
    ],
  };
}

export function builtInCustomRuleTemplates(): CustomRuleDefinition[] {
  return [
    {
      id: 'TPL-BUILTIN-NO-DELAY',
      name: 'No Delay Activities',
      description: 'Detects workflows that still use fixed Delay activities.',
      recommendation: 'Prefer Retry Scope, Check App State, or timeout-based UI activities.',
      category: 'Reliability',
      severity: 'Warning',
      scope: 'Activity',
      enabled: false,
      isTemplate: true,
      templateSource: 'BuiltIn',
      weight: 2,
      maxPenalty: 10,
      matchMode: 'All',
      conditions: [
        { field: 'Activity.Name', operator: 'Equals', value: 'Delay', caseSensitive: false },
      ],
    },
    {
      id: 'TPL-BUILTIN-CONTINUE-ON-ERROR',
      name: 'ContinueOnError Usage',
      description: 'Detects executable activities where ContinueOnError is enabled.',
      recommendation: 'Handle expected exceptions explicitly instead of suppressing failures.',
      category: 'ExceptionHandling',
      severity: 'Warning',
      scope: 'Activity',
      enabled: false,
      isTemplate: true,
      templateSource: 'BuiltIn',
      weight: 4,
      maxPenalty: 12,
      matchMode: 'All',
      conditions: [
        { field: 'Activity.Property', operator: 'Equals', propertyName: 'ContinueOnError', value: 'True', compareValue: 'True', caseSensitive: false },
      ],
    },
    {
      id: 'TPL-BUILTIN-LARGE-WORKFLOW',
      name: 'Large Workflow',
      description: 'Detects workflows with high executable activity count.',
      recommendation: 'Split large workflows into focused reusable components.',
      category: 'Maintainability',
      severity: 'Warning',
      scope: 'Workflow',
      enabled: false,
      isTemplate: true,
      templateSource: 'BuiltIn',
      weight: 5,
      maxPenalty: 15,
      matchMode: 'All',
      conditions: [
        { field: 'Workflow.ExecutableActivityCount', operator: 'GreaterThanOrEqual', value: '100', caseSensitive: false },
      ],
    },
  ];
}

export function createRuleDraftFromTemplate(template: CustomRuleDefinition): CustomRuleDefinition {
  return {
    ...template,
    id: `CUSTOM-${Date.now().toString().slice(-6)}`,
    enabled: true,
    isTemplate: false,
    templateId: template.id,
    templateSource: template.templateSource ?? (template.id.startsWith('TPL-BUILTIN-') ? 'BuiltIn' : 'Custom'),
    createdAtUtc: null,
    updatedAtUtc: null,
    conditions: template.conditions.map((condition) => ({ ...condition })),
  };
}

export function createCustomTemplateId(rule: CustomRuleDefinition): string {
  return `TPL-CUSTOM-${slugifyRuleId(rule.name || rule.id)}-${Date.now().toString().slice(-6)}`;
}

export function slugifyRuleId(value: string): string {
  const slug = value.trim().toUpperCase().replace(/[^A-Z0-9]+/g, '-').replace(/^-+|-+$/g, '');
  return slug || 'RULE';
}

export function upsertCustomRule(rules: CustomRuleDefinition[], rule: CustomRuleDefinition): CustomRuleDefinition[] {
  return [...rules.filter((item) => item.id !== rule.id), rule].sort((left, right) => left.id.localeCompare(right.id));
}

export function createDefaultRuleProfile(rules: RuleCatalogItem[]): RuleProfile {
  return {
    id: `custom-profile-${Date.now().toString().slice(-6)}`,
    name: 'Company Standard',
    description: 'Local custom profile for company-specific rule configuration.',
    rules: rules.map((rule) => ({
      ruleId: rule.id,
      enabled: rule.enabledByDefault ?? true,
      severityOverride: null,
      weight: rule.defaultWeight ?? 1,
      maxPenalty: rule.defaultMaxPenalty ?? 10,
      description: rule.name,
    })),
  };
}

export function createFallbackProfileFromRules(rules: RuleCatalogItem[]): RuleProfile {
  return {
    id: 'default',
    name: 'Default',
    description: 'Default profile derived from the loaded rule catalog.',
    rules: rules
      .filter((rule) => rule.enabledByDefault !== false)
      .map((rule) => ({
        ruleId: rule.id,
        enabled: true,
        severityOverride: null,
        weight: rule.defaultWeight ?? 1,
        maxPenalty: rule.defaultMaxPenalty ?? 10,
        description: rule.name,
      })),
  };
}

export function ruleCategories(): string[] {
  return ['Reliability', 'Maintainability', 'Performance', 'Security', 'ExceptionHandling', 'Naming', 'Logging', 'UiAutomation', 'Architecture', 'Configuration', 'Orchestrator'];
}

export function ruleSeverities(): string[] {
  return ['Info', 'Suggestion', 'Warning', 'Error', 'Critical'];
}

export function ruleScopes(): string[] {
  return ['Activity', 'Workflow', 'Project'];
}

export function conditionFields(): string[] {
  return [
    'Activity.Name',
    'Activity.DisplayName',
    'Activity.Property',
    'Workflow.Name',
    'Workflow.Path',
    'Workflow.ActivityCount',
    'Workflow.ExecutableActivityCount',
    'Workflow.MaxNestingDepth',
    'Project.Compatibility',
    'Project.IsReFramework',
    'Dependency.Name',
    'Dependency.Version',
    'Dependency.Category',
    'Dependency.UsageStatus',
    'Dependency.RiskLevel',
  ];
}

export function conditionOperators(): string[] {
  return ['Equals', 'NotEquals', 'Contains', 'StartsWith', 'EndsWith', 'GreaterThan', 'GreaterThanOrEqual', 'LessThan', 'LessThanOrEqual', 'Exists', 'NotExists'];
}

export function updateCondition(
  setDraft: React.Dispatch<React.SetStateAction<CustomRuleDefinition>>,
  index: number,
  patch: Partial<CustomRuleDefinition['conditions'][number]>,
): void {
  setDraft((rule) => ({
    ...rule,
    conditions: rule.conditions.map((condition, currentIndex) => currentIndex === index ? { ...condition, ...patch } : condition),
  }));
}

export function removeCondition(setDraft: React.Dispatch<React.SetStateAction<CustomRuleDefinition>>, index: number): void {
  setDraft((rule) => ({
    ...rule,
    conditions: rule.conditions.filter((_, currentIndex) => currentIndex !== index),
  }));
}

export function unique(values: string[]): string[] {
  return Array.from(new Set(values.filter(Boolean)));
}

export function uniqueTemplates(templates: CustomRuleDefinition[]): CustomRuleDefinition[] {
  const seen = new Set<string>();
  return templates.filter((template) => {
    const key = template.id.toLowerCase();
    if (seen.has(key)) {
      return false;
    }

    seen.add(key);
    return true;
  });
}

import React from 'react';
import {
  Braces,
  CheckCircle2,
  CirclePause,
  Code2,
  Download,
  Pencil,
  Plus,
  RefreshCw,
  Search,
  ShieldCheck,
  Upload,
} from 'lucide-react';
import type {
  CustomRuleDefinition,
  CustomRuleTestResult,
  RuleCatalogItem,
  RuleProfile,
} from '../services/reportViewModel';
import {
  exportCustomRules,
  exportRuleProfiles,
  getCustomRules,
  importCustomRules,
  saveCustomRule,
  saveRuleProfile,
  testCustomRule,
} from '../services/apiClient';
import {
  Metric,
  builtInCustomRuleTemplates,
  conditionFields,
  conditionOperators,
  createCustomTemplateId,
  createDefaultCustomRule,
  createDefaultRuleProfile,
  createFallbackProfileFromRules,
  createRuleDraftFromTemplate,
  downloadJson,
  removeCondition,
  ruleCategories,
  ruleScopes,
  ruleSeverities,
  unique,
  uniqueTemplates,
  updateCondition,
  upsertCustomRule,
} from './uiUtils';

export function RulesView({
  rules,
  isLoading,
  selectedRule,
  projectPath,
  profiles,
  selectedProfileId,
  status,
  t,
  onSelectRule,
  onRefresh,
  onSelectProfileId,
  onStatus,
}: {
  rules: RuleCatalogItem[];
  isLoading: boolean;
  selectedRule: RuleCatalogItem | null;
  projectPath: string;
  profiles: RuleProfile[];
  selectedProfileId: string;
  status: string;
  t: (key: string, values?: Record<string, unknown>) => string;
  onSelectRule: (rule: RuleCatalogItem) => void;
  onRefresh: () => void;
  onSelectProfileId: (profileId: string) => void;
  onStatus: (message: string) => void;
}) {
  const [query, setQuery] = React.useState('');
  const [category, setCategory] = React.useState('All');
  const [severity, setSeverity] = React.useState('All');
  const [sourceFilter, setSourceFilter] = React.useState('All');
  const [templateSourceFilter, setTemplateSourceFilter] = React.useState('All');
  const [customRules, setCustomRules] = React.useState<CustomRuleDefinition[]>([]);
  const [draft, setDraft] = React.useState<CustomRuleDefinition>(() => createDefaultCustomRule());
  const [profileDraft, setProfileDraft] = React.useState<RuleProfile>(() =>
    createDefaultRuleProfile([]),
  );
  const [testResult, setTestResult] = React.useState<CustomRuleTestResult | null>(null);
  const [isTesting, setIsTesting] = React.useState(false);
  const [isSaving, setIsSaving] = React.useState(false);
  const [rulesTab, setRulesTab] = React.useState<'active' | 'profile' | 'custom'>('active');
  const fileInputRef = React.useRef<HTMLInputElement | null>(null);
  const builtInTemplates = React.useMemo(() => builtInCustomRuleTemplates(), []);
  const customTemplates = React.useMemo(
    () => customRules.filter((rule) => rule.isTemplate),
    [customRules],
  );
  const loadCustomRules = React.useCallback(async () => {
    try {
      const result = (await getCustomRules()) as CustomRuleDefinition[];
      setCustomRules(result);
    } catch {
      setCustomRules([]);
    }
  }, []);
  React.useEffect(() => {
    if (rules.length > 0 && profileDraft.rules.length === 0) {
      setProfileDraft(createDefaultRuleProfile(rules));
    }
  }, [profileDraft.rules.length, rules]);
  React.useEffect(() => {
    void loadCustomRules();
  }, [loadCustomRules]);
  const activeProfile =
    profiles.find((profile) => profile.id === selectedProfileId) ??
    profiles.find((profile) => profile.id === 'default') ??
    createFallbackProfileFromRules(rules);
  const configuredRules =
    activeProfile.rules.length > 0
      ? activeProfile.rules
      : rules
          .filter((rule) => rule.enabledByDefault !== false)
          .map((rule) => ({
            ruleId: rule.id,
            enabled: true,
            severityOverride: null,
            weight: rule.defaultWeight ?? 1,
            maxPenalty: rule.defaultMaxPenalty ?? 10,
            description: rule.name,
          }));
  const configuredRuleRows = configuredRules.map((configuration) => {
    const catalogRule = rules.find((rule) => rule.id === configuration.ruleId);
    return { configuration, catalogRule };
  });
  const activeRuleRows = configuredRuleRows.filter((row) => row.configuration.enabled);
  const profileTemplates = React.useMemo(
    () =>
      activeRuleRows
        .map((row) => {
          const fullCustomRule = customRules.find(
            (rule) => rule.id === row.configuration.ruleId && !rule.isTemplate,
          );
          if (!fullCustomRule) {
            return null;
          }

          return {
            ...fullCustomRule,
            id: fullCustomRule.templateId ?? `PROFILE-${fullCustomRule.id}`,
            name: fullCustomRule.name,
            enabled: false,
            isTemplate: true,
            templateSource: 'Profile',
          };
        })
        .filter(Boolean) as CustomRuleDefinition[],
    [activeRuleRows, customRules],
  );
  const ruleTemplates = React.useMemo(() => {
    const builtIns = builtInTemplates.map((template) => ({
      ...template,
      templateSource: 'BuiltIn',
    }));
    const mine = customTemplates.map((template) => ({ ...template, templateSource: 'Custom' }));
    const profile = profileTemplates.map((template) => ({
      ...template,
      templateSource: 'Profile',
    }));
    if (templateSourceFilter === 'BuiltIn') {
      return builtIns;
    }

    if (templateSourceFilter === 'Custom') {
      return mine;
    }

    if (templateSourceFilter === 'Profile') {
      return profile;
    }

    return uniqueTemplates([...builtIns, ...profile, ...mine]);
  }, [builtInTemplates, customTemplates, profileTemplates, templateSourceFilter]);
  const categories = unique([
    'All',
    ...(configuredRuleRows
      .map((row) => row.catalogRule?.category)
      .filter(Boolean) as string[]),
  ]);
  const severities = unique([
    'All',
    ...(configuredRuleRows
      .map((row) => row.configuration.severityOverride ?? row.catalogRule?.defaultSeverity)
      .filter(Boolean) as string[]),
  ]);
  const filtered = configuredRuleRows.filter((row) => {
    const rule = row.catalogRule;
    const ruleName = rule?.name ?? row.configuration.description ?? row.configuration.ruleId;
    const isBuiltIn = rule?.isBuiltIn ?? row.configuration.ruleId.toUpperCase().startsWith('RPA');
    const sourceMatches =
      sourceFilter === 'All' ||
      (sourceFilter === 'BuiltIn' && isBuiltIn) ||
      (sourceFilter === 'Custom' && !isBuiltIn);
    const q = query.trim().toLowerCase();
    return (
      (!q ||
        row.configuration.ruleId.toLowerCase().includes(q) ||
        ruleName.toLowerCase().includes(q)) &&
      (category === 'All' || rule?.category === category) &&
      (severity === 'All' ||
        (row.configuration.severityOverride ?? rule?.defaultSeverity) === severity) &&
      sourceMatches
    );
  });
  const selectedActiveRow = selectedRule
    ? configuredRuleRows.find((row) => row.configuration.ruleId === selectedRule.id)
    : null;
  const disabledRuleCount = configuredRuleRows.length - activeRuleRows.length;
  const customRuleCount = configuredRuleRows.filter(
    (row) =>
      !(
        row.catalogRule?.isBuiltIn ?? row.configuration.ruleId.toUpperCase().startsWith('RPA')
      ),
  ).length;
  const hasActiveFilters =
    Boolean(query.trim()) || category !== 'All' || severity !== 'All' || sourceFilter !== 'All';

  function clearActiveRuleFilters() {
    setQuery('');
    setCategory('All');
    setSeverity('All');
    setSourceFilter('All');
  }

  async function runTestRule() {
    if (!projectPath) {
      onStatus(t('customRuleNeedsProject'));
      return;
    }

    setIsTesting(true);
    try {
      const result = (await testCustomRule(projectPath, draft)) as CustomRuleTestResult;
      setTestResult(result);
      onStatus(
        result.hasNoiseWarning
          ? result.noiseWarning ?? t('ruleNoiseWarning')
          : t('customRuleTestCompleted'),
      );
    } catch (error) {
      onStatus(error instanceof Error ? error.message : t('customRuleTestFailed'));
    } finally {
      setIsTesting(false);
    }
  }

  async function saveRule() {
    setIsSaving(true);
    try {
      await saveCustomRule({ ...draft, isTemplate: false });
      onStatus(t('customRuleSaved'));
      setDraft(createDefaultCustomRule());
      setTestResult(null);
      await loadCustomRules();
      onRefresh();
    } catch (error) {
      onStatus(error instanceof Error ? error.message : t('customRuleSaveFailed'));
    } finally {
      setIsSaving(false);
    }
  }

  async function saveTemplate() {
    const templateName = draft.name.trim();
    if (
      customTemplates.some(
        (template) => template.name.trim().toLowerCase() === templateName.toLowerCase(),
      )
    ) {
      onStatus(t('ruleTemplateDuplicate'));
      return;
    }

    setIsSaving(true);
    try {
      const template = {
        ...draft,
        id: createCustomTemplateId(draft),
        enabled: false,
        isTemplate: true,
        templateId: null,
        templateSource: 'Custom',
      };
      const saved = (await saveCustomRule(template)) as CustomRuleDefinition;
      setCustomRules((current) => upsertCustomRule(current, saved));
      onStatus(t('ruleTemplateSaved', { templateName: saved.name || templateName }));
    } catch {
      onStatus(t('ruleTemplateSaveFailed'));
    } finally {
      setIsSaving(false);
    }
  }

  async function saveProfile() {
    setIsSaving(true);
    try {
      await saveRuleProfile(profileDraft);
      onStatus(t('ruleProfileSaved'));
      onRefresh();
    } catch (error) {
      onStatus(error instanceof Error ? error.message : t('ruleProfileSaveFailed'));
    } finally {
      setIsSaving(false);
    }
  }

  async function downloadProfiles() {
    const store = await exportRuleProfiles();
    downloadJson('rule-profiles.json', store);
    onStatus(t('ruleProfilesExported'));
  }

  async function downloadRules() {
    const store = await exportCustomRules();
    downloadJson('custom-rules.json', store);
    onStatus(t('customRulesExported'));
  }

  async function importRulesFromPrompt() {
    fileInputRef.current?.click();
  }

  async function importRulesFromFile(file: File) {
    try {
      const raw = await file.text();
      const parsed = JSON.parse(raw) as { rules?: unknown[] } | unknown[];
      const rulesToImport = Array.isArray(parsed) ? parsed : parsed.rules ?? [];
      await importCustomRules(rulesToImport, false);
      onStatus(t('customRulesImported'));
      await loadCustomRules();
      onRefresh();
    } catch {
      onStatus(t('customRulesImportFailed'));
    }
  }

  return (
    <section className="results-panel rules-panel" aria-label={t('rules')}>
      <div className="section-heading rules-page-heading">
        <div>
          <span className="eyebrow">{t('ruleManagement')}</span>
          <h2>{t('ruleCatalog')}</h2>
          <p className="hint">{t('rulesWorkspaceHelp')}</p>
        </div>
        <div className="action-row compact-actions">
          <button type="button" className="icon-text-button" onClick={onRefresh}>
            <RefreshCw size={16} />
            {t('refresh')}
          </button>
          <button type="button" className="icon-text-button" onClick={() => void downloadRules()}>
            <Download size={16} />
            {t('exportRules')}
          </button>
          <button
            type="button"
            className="icon-text-button"
            onClick={() => void importRulesFromPrompt()}
          >
            <Upload size={16} />
            {t('importRules')}
          </button>
          <button
            type="button"
            className="primary-action icon-text-button"
            onClick={() => setRulesTab('custom')}
          >
            <Plus size={16} />
            {t('newCustomRule')}
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept="application/json,.json"
            hidden
            onChange={(event) => {
              const file = event.target.files?.[0];
              event.target.value = '';
              if (file) {
                void importRulesFromFile(file);
              }
            }}
          />
        </div>
      </div>
      {status && <span className="sr-only" role="status" aria-label={status} />}
      <span className="sr-only">
        {t('activeRulesHint', { profile: activeProfile?.name ?? selectedProfileId })}
      </span>
      <nav className="tabs compact-tabs" aria-label={t('rules')}>
        <button
          type="button"
          className={rulesTab === 'active' ? 'active' : ''}
          onClick={() => setRulesTab('active')}
        >
          {t('activeRules')}
        </button>
        <button
          type="button"
          className={rulesTab === 'profile' ? 'active' : ''}
          onClick={() => setRulesTab('profile')}
        >
          {t('profileBuilder')}
        </button>
        <button
          type="button"
          className={rulesTab === 'custom' ? 'active' : ''}
          onClick={() => setRulesTab('custom')}
        >
          {t('createRule')}
        </button>
      </nav>
      {rulesTab === 'active' ? (
        <>
          <div className="rule-catalog-summary">
            <div className="rule-summary-tile">
              <span className="rule-summary-icon icon-total">
                <ShieldCheck size={18} />
              </span>
              <div>
                <span>{t('totalRules')}</span>
                <strong>{configuredRuleRows.length}</strong>
                <small>{t('totalRulesHint')}</small>
              </div>
            </div>
            <div className="rule-summary-tile">
              <span className="rule-summary-icon icon-enabled">
                <CheckCircle2 size={18} />
              </span>
              <div>
                <span>{t('enabledRules')}</span>
                <strong>{activeRuleRows.length}</strong>
                <small>{t('enabledRulesHint')}</small>
              </div>
            </div>
            <div className="rule-summary-tile">
              <span className="rule-summary-icon icon-disabled">
                <CirclePause size={18} />
              </span>
              <div>
                <span>{t('disabledRules')}</span>
                <strong>{disabledRuleCount}</strong>
                <small>{t('disabledRulesHint')}</small>
              </div>
            </div>
            <div className="rule-summary-tile">
              <span className="rule-summary-icon icon-custom">
                <Code2 size={18} />
              </span>
              <div>
                <span>{t('customRules')}</span>
                <strong>{customRuleCount}</strong>
                <small>{t('customRulesHint')}</small>
              </div>
            </div>
          </div>
          <div className="rule-profile-summary compact-profile-summary">
            <label className="rule-profile-picker">
              <span>{t('activeProfile')}</span>
              <select
                aria-label={t('profileTemplate')}
                value={selectedProfileId}
                onChange={(event) => onSelectProfileId(event.target.value)}
              >
                {(profiles.length > 0 ? profiles : [activeProfile]).map((profile) => (
                  <option key={profile.id} value={profile.id}>
                    {profile.name}
                  </option>
                ))}
              </select>
              <small>{t('projectProfileHint')}</small>
            </label>
            <button
              type="button"
              className="icon-text-button"
              onClick={() => setRulesTab('profile')}
            >
              <Pencil size={15} />
              {t('editProfile')}
            </button>
          </div>
          <div className="rules-filter-toolbar">
            <label className="rules-search-control">
              <Search size={17} aria-hidden="true" />
              <input
                aria-label={t('searchRules')}
                placeholder={t('searchRules')}
                value={query}
                onChange={(event) => setQuery(event.target.value)}
              />
            </label>
            <select
              aria-label={t('category')}
              value={category}
              onChange={(event) => setCategory(event.target.value)}
            >
              {categories.map((item) => (
                <option key={item}>{item === 'All' ? t('allCategories') : item}</option>
              ))}
            </select>
            <select
              aria-label={t('severity')}
              value={severity}
              onChange={(event) => setSeverity(event.target.value)}
            >
              {severities.map((item) => (
                <option key={item}>{item === 'All' ? t('allSeverities') : item}</option>
              ))}
            </select>
            <select
              aria-label={t('ruleSource')}
              value={sourceFilter}
              onChange={(event) => setSourceFilter(event.target.value)}
            >
              <option value="All">{t('allSources')}</option>
              <option value="BuiltIn">{t('builtInRules')}</option>
              <option value="Custom">{t('customRules')}</option>
            </select>
            <button
              type="button"
              className="rules-clear-button"
              disabled={!hasActiveFilters}
              onClick={clearActiveRuleFilters}
            >
              {t('clearFilters')}
            </button>
          </div>
          <div className="rules-results-summary">
            <strong>{t('filteredRuleCount', { count: filtered.length })}</strong>
            <span>{t('profileConfiguredRuleCount', { count: configuredRuleRows.length })}</span>
          </div>
          <div className="rules-workspace">
            <div className="rules-table-shell">
              {isLoading ? (
                <p className="empty-state">{t('loadingRules')}</p>
              ) : filtered.length === 0 ? (
                <p className="empty-state">{t('noActiveRulesMatchFilter')}</p>
              ) : (
                <table className="compact-table rules-table">
                  <thead>
                    <tr>
                      <th>{t('ruleId')}</th>
                      <th>{t('ruleName')}</th>
                      <th>{t('category')}</th>
                      <th>{t('effectiveSeverity')}</th>
                      <th>{t('status')}</th>
                      <th>{t('scoreImpact')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filtered.map(({ configuration, catalogRule }) => {
                      const rule = catalogRule;
                      const displayName =
                        rule?.name ?? configuration.description ?? configuration.ruleId;
                      const isBuiltIn =
                        rule?.isBuiltIn ?? configuration.ruleId.toUpperCase().startsWith('RPA');
                      return (
                        <tr
                          key={configuration.ruleId}
                          tabIndex={rule ? 0 : undefined}
                          onKeyDown={(event) => {
                            if (rule && (event.key === 'Enter' || event.key === ' '))
                              onSelectRule(rule);
                          }}
                          onClick={() => rule && onSelectRule(rule)}
                          className={selectedRule?.id === configuration.ruleId ? 'selected-row' : ''}
                        >
                          <td>
                            <span className="rule-id-label">{configuration.ruleId}</span>
                          </td>
                          <td>
                            <strong>{displayName}</strong>
                            <span className="rule-cell-note">
                              {isBuiltIn ? t('builtIn') : t('custom')}
                            </span>
                          </td>
                          <td>
                            <span className="rule-category-label">{rule?.category ?? '-'}</span>
                          </td>
                          <td>
                            <span
                              className={`rule-severity-badge severity-${(configuration.severityOverride ?? rule?.defaultSeverity ?? 'info').toLowerCase()}`}
                            >
                              {configuration.severityOverride ?? rule?.defaultSeverity ?? '-'}
                            </span>
                          </td>
                          <td>
                            <span
                              className={`rule-status-indicator ${configuration.enabled ? 'is-enabled' : 'is-disabled'}`}
                            >
                              <span />
                              {configuration.enabled ? t('enabled') : t('disabled')}
                            </span>
                          </td>
                          <td>
                            <strong>{configuration.weight}</strong>
                            <span className="rule-cell-note">
                              {t('maxPenaltyShort', { value: configuration.maxPenalty })}
                            </span>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              )}
            </div>
            <aside className="detail-panel rule-detail-panel">
              {selectedRule ? (
                <>
                  <div className="rule-detail-header">
                    <div>
                      <span className="eyebrow">
                        {selectedRule.isBuiltIn ? t('builtInRule') : t('customRule')}
                      </span>
                      <h3>
                        {selectedRule.id} {selectedRule.name}
                      </h3>
                    </div>
                    <span
                      className={`rule-severity-badge severity-${(selectedActiveRow?.configuration.severityOverride ?? selectedRule.defaultSeverity).toLowerCase()}`}
                    >
                      {selectedActiveRow?.configuration.severityOverride ??
                        selectedRule.defaultSeverity}
                    </span>
                  </div>
                  <section className="rule-detail-copy">
                    <h4>{t('description')}</h4>
                    <p>{selectedRule.description || '-'}</p>
                  </section>
                  {selectedRule.recommendation && (
                    <section className="rule-detail-copy">
                      <h4>{t('recommendation')}</h4>
                      <p>{selectedRule.recommendation}</p>
                    </section>
                  )}
                  <section className="rule-detail-copy">
                    <h4>{t('projectCompatibility')}</h4>
                    <div className="rule-project-types">
                      {selectedRule.applicableProjectTypes &&
                      selectedRule.applicableProjectTypes.length > 0 ? (
                        selectedRule.applicableProjectTypes.map((projectType) => (
                          <span key={projectType}>{projectType}</span>
                        ))
                      ) : (
                        <span>{t('allProjectTypes')}</span>
                      )}
                    </div>
                    {selectedRule.compatibilityNotes && (
                      <p className="rule-compatibility-note">{selectedRule.compatibilityNotes}</p>
                    )}
                  </section>
                  <div className="metric-grid compact rule-detail-metrics">
                    <Metric label={t('category')} value={selectedRule.category} />
                    <Metric
                      label={t('effectiveSeverity')}
                      value={
                        selectedActiveRow?.configuration.severityOverride ??
                        selectedRule.defaultSeverity
                      }
                    />
                    <Metric label={t('scope')} value={selectedRule.scope} />
                    <Metric
                      label={t('weight')}
                      value={
                        selectedActiveRow?.configuration.weight ??
                        selectedRule.defaultWeight ??
                        0
                      }
                    />
                    <Metric
                      label={t('maxPenalty')}
                      value={
                        selectedActiveRow?.configuration.maxPenalty ??
                        selectedRule.defaultMaxPenalty ??
                        0
                      }
                    />
                    <Metric
                      label={t('aggregationSupport')}
                      value={selectedRule.supportsAggregation ? t('yes') : t('no')}
                    />
                    <Metric
                      label={t('fixSupport')}
                      value={selectedRule.hasFixSuggestion ? t('yes') : t('no')}
                    />
                    <Metric
                      label={t('autoApply')}
                      value={selectedRule.canAutoApply ? t('yes') : t('no')}
                    />
                  </div>
                  <div className="rule-detail-actions">
                    <button
                      type="button"
                      className="icon-text-button"
                      onClick={() => setRulesTab('profile')}
                    >
                      <Pencil size={15} />
                      {t('editProfile')}
                    </button>
                  </div>
                </>
              ) : (
                <div className="rule-detail-empty">
                  <Braces size={30} />
                  <p>{t('selectRule')}</p>
                </div>
              )}
            </aside>
          </div>
        </>
      ) : rulesTab === 'profile' ? (
        <RuleProfileBuilder
          profiles={profiles.length > 0 ? profiles : [activeProfile]}
          rules={rules}
          draft={profileDraft}
          setDraft={setProfileDraft}
          t={t}
          onSave={() => void saveProfile()}
          onExport={() => void downloadProfiles()}
        />
      ) : (
        <CustomRuleBuilder
          draft={draft}
          setDraft={setDraft}
          testResult={testResult}
          isTesting={isTesting}
          isSaving={isSaving}
          templates={ruleTemplates}
          templateSourceFilter={templateSourceFilter}
          customTemplateCount={customTemplates.length}
          t={t}
          onTemplateSourceFilterChange={setTemplateSourceFilter}
          onLoadTemplate={(template) => {
            setDraft(createRuleDraftFromTemplate(template));
            setTestResult(null);
          }}
          onTest={() => void runTestRule()}
          onSave={() => void saveRule()}
          onSaveTemplate={() => void saveTemplate()}
        />
      )}
    </section>
  );
}

export function RuleProfileBuilder({
  profiles,
  rules,
  draft,
  setDraft,
  t,
  onSave,
  onExport,
}: {
  profiles: RuleProfile[];
  rules: RuleCatalogItem[];
  draft: RuleProfile;
  setDraft: React.Dispatch<React.SetStateAction<RuleProfile>>;
  t: (key: string, values?: Record<string, unknown>) => string;
  onSave: () => void;
  onExport: () => void;
}) {
  function updateRule(ruleId: string, patch: Partial<RuleProfile['rules'][number]>) {
    setDraft((profile) => ({
      ...profile,
      rules: profile.rules.map((rule) =>
        rule.ruleId === ruleId ? { ...rule, ...patch } : rule,
      ),
    }));
  }

  function updateNamingConvention(
    ruleId: string,
    field: 'pattern' | 'requiredPrefix' | 'inPrefix' | 'outPrefix' | 'inOutPrefix',
    value: string,
  ) {
    setDraft((profile) => ({
      ...profile,
      rules: profile.rules.map((rule) =>
        rule.ruleId === ruleId
          ? {
              ...rule,
              namingConvention: {
                ...rule.namingConvention,
                [field]: value || null,
              },
            }
          : rule,
      ),
    }));
  }

  return (
    <section className="builder-panel">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t('customRuleProfiles')}</span>
          <h2>{t('profileBuilder')}</h2>
        </div>
        <div className="action-row compact-actions">
          <button type="button" onClick={onExport}>
            {t('exportProfiles')}
          </button>
          <button className="primary-action" type="button" onClick={onSave}>
            {t('saveProfile')}
          </button>
        </div>
      </div>
      <div className="builder-grid">
        <label>
          {t('profileId')}
          <input
            value={draft.id}
            onChange={(event) =>
              setDraft((profile) => ({ ...profile, id: event.target.value }))
            }
          />
        </label>
        <label>
          {t('profileName')}
          <input
            value={draft.name}
            onChange={(event) =>
              setDraft((profile) => ({ ...profile, name: event.target.value }))
            }
          />
        </label>
        <label className="wide-field">
          {t('description')}
          <textarea
            value={draft.description ?? ''}
            onChange={(event) =>
              setDraft((profile) => ({ ...profile, description: event.target.value }))
            }
          />
        </label>
        <label className="wide-field">
          {t('profileTemplate')}
          <select
            value=""
            onChange={(event) => {
              const selected = profiles.find((profile) => profile.id === event.target.value);
              if (selected) {
                setDraft({
                  ...selected,
                  id: selected.id === 'default' ? `custom-${Date.now()}` : selected.id,
                  name: selected.id === 'default' ? `${selected.name} Custom` : selected.name,
                });
              }
            }}
          >
            <option value="">{t('selectProfileTemplate')}</option>
            {profiles.map((profile) => (
              <option key={profile.id} value={profile.id}>
                {profile.name}
              </option>
            ))}
          </select>
        </label>
      </div>
      <div className="table-scroll">
        <table className="compact-table">
          <thead>
            <tr>
              <th>{t('ruleId')}</th>
              <th>{t('enabled')}</th>
              <th>{t('severityOverride')}</th>
              <th>{t('weight')}</th>
              <th>{t('maxPenalty')}</th>
            </tr>
          </thead>
          <tbody>
            {draft.rules.map((rule) => {
              const catalogRule = rules.find((item) => item.id === rule.ruleId);
              const supportsNamingConvention = ['RPA006', 'RPA031', 'RPA032'].includes(
                rule.ruleId.toUpperCase(),
              );
              return (
                <React.Fragment key={rule.ruleId}>
                  <tr>
                    <td>
                      {rule.ruleId} {catalogRule?.name ?? rule.description}
                    </td>
                    <td>
                      <input
                        type="checkbox"
                        checked={rule.enabled}
                        onChange={(event) =>
                          updateRule(rule.ruleId, { enabled: event.target.checked })
                        }
                      />
                    </td>
                    <td>
                      <select
                        value={rule.severityOverride ?? ''}
                        onChange={(event) =>
                          updateRule(rule.ruleId, {
                            severityOverride: event.target.value || null,
                          })
                        }
                      >
                        <option value="">{t('defaultSeverity')}</option>
                        {ruleSeverities().map((item) => (
                          <option key={item}>{item}</option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <input
                        type="number"
                        min="0"
                        value={rule.weight}
                        onChange={(event) =>
                          updateRule(rule.ruleId, { weight: Number(event.target.value) })
                        }
                      />
                    </td>
                    <td>
                      <input
                        type="number"
                        min="0"
                        value={rule.maxPenalty}
                        onChange={(event) =>
                          updateRule(rule.ruleId, { maxPenalty: Number(event.target.value) })
                        }
                      />
                    </td>
                  </tr>
                  {supportsNamingConvention && (
                    <tr className="rule-configuration-row">
                      <td colSpan={5}>
                        <div className="builder-grid" role="group" aria-label={`${rule.ruleId} ${t('namingConventionSettings')}`}>
                          <label>
                            {t('namingPattern')}
                            <input
                              aria-label={`${rule.ruleId} ${t('namingPattern')}`}
                              value={rule.namingConvention?.pattern ?? ''}
                              placeholder={t('namingPatternPlaceholder')}
                              onChange={(event) =>
                                updateNamingConvention(rule.ruleId, 'pattern', event.target.value)
                              }
                            />
                          </label>
                          {rule.ruleId.toUpperCase() !== 'RPA031' && (
                            <label>
                              {t('requiredPrefix')}
                              <input
                                aria-label={`${rule.ruleId} ${t('requiredPrefix')}`}
                                value={rule.namingConvention?.requiredPrefix ?? ''}
                                onChange={(event) =>
                                  updateNamingConvention(rule.ruleId, 'requiredPrefix', event.target.value)
                                }
                              />
                            </label>
                          )}
                          {rule.ruleId.toUpperCase() === 'RPA031' && (
                            <>
                              <label>
                                {t('inArgumentPrefix')}
                                <input
                                  aria-label={`${rule.ruleId} ${t('inArgumentPrefix')}`}
                                  value={rule.namingConvention?.inPrefix ?? ''}
                                  onChange={(event) =>
                                    updateNamingConvention(rule.ruleId, 'inPrefix', event.target.value)
                                  }
                                />
                              </label>
                              <label>
                                {t('outArgumentPrefix')}
                                <input
                                  aria-label={`${rule.ruleId} ${t('outArgumentPrefix')}`}
                                  value={rule.namingConvention?.outPrefix ?? ''}
                                  onChange={(event) =>
                                    updateNamingConvention(rule.ruleId, 'outPrefix', event.target.value)
                                  }
                                />
                              </label>
                              <label>
                                {t('inOutArgumentPrefix')}
                                <input
                                  aria-label={`${rule.ruleId} ${t('inOutArgumentPrefix')}`}
                                  value={rule.namingConvention?.inOutPrefix ?? ''}
                                  onChange={(event) =>
                                    updateNamingConvention(rule.ruleId, 'inOutPrefix', event.target.value)
                                  }
                                />
                              </label>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  )}
                </React.Fragment>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}

export function CustomRuleBuilder({
  draft,
  setDraft,
  testResult,
  isTesting,
  isSaving,
  templates,
  templateSourceFilter,
  customTemplateCount,
  t,
  onTemplateSourceFilterChange,
  onLoadTemplate,
  onTest,
  onSave,
  onSaveTemplate,
}: {
  draft: CustomRuleDefinition;
  setDraft: React.Dispatch<React.SetStateAction<CustomRuleDefinition>>;
  testResult: CustomRuleTestResult | null;
  isTesting: boolean;
  isSaving: boolean;
  templates: CustomRuleDefinition[];
  templateSourceFilter: string;
  customTemplateCount: number;
  t: (key: string, values?: Record<string, unknown>) => string;
  onTemplateSourceFilterChange: (value: string) => void;
  onLoadTemplate: (template: CustomRuleDefinition) => void;
  onTest: () => void;
  onSave: () => void;
  onSaveTemplate: () => void;
}) {
  return (
    <section className="builder-panel">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t('customRuleBuilder')}</span>
          <h2>{t('createRule')}</h2>
        </div>
        <div className="action-row compact-actions">
          <button type="button" onClick={onTest} disabled={isTesting}>
            {isTesting ? t('testing') : t('testRule')}
          </button>
          <button type="button" onClick={onSaveTemplate} disabled={isSaving}>
            {isSaving ? t('saving') : t('saveAsTemplate')}
          </button>
          <button
            className="primary-action"
            type="button"
            onClick={onSave}
            disabled={isSaving}
          >
            {isSaving ? t('saving') : t('saveRule')}
          </button>
        </div>
      </div>
      <div className="template-panel">
        <div className="section-heading compact-heading">
          <div>
            <span className="eyebrow">{t('ruleTemplates')}</span>
            <h3>{t('reuseRuleTemplate')}</h3>
          </div>
          <select
            aria-label={t('templateSource')}
            value={templateSourceFilter}
            onChange={(event) => onTemplateSourceFilterChange(event.target.value)}
          >
            <option value="All">{t('allTemplates')}</option>
            <option value="BuiltIn">{t('builtInTemplates')}</option>
            <option value="Profile">{t('analysisProfileTemplates')}</option>
            <option value="Custom">{t('myTemplates')}</option>
          </select>
        </div>
        {templateSourceFilter === 'Custom' && customTemplateCount === 0 ? (
          <p className="empty-state">{t('noCustomTemplates')}</p>
        ) : templates.length === 0 ? (
          <p className="empty-state">{t('noTemplatesMatchFilter')}</p>
        ) : (
          <div className="template-grid">
            {templates.map((template) => (
              <button
                type="button"
                className="template-card"
                key={template.id}
                onClick={() => onLoadTemplate(template)}
              >
                <span className="eyebrow">
                  {template.templateSource === 'Custom'
                    ? t('myTemplates')
                    : template.templateSource === 'Profile'
                      ? t('analysisProfileTemplates')
                      : t('builtInTemplates')}
                </span>
                <strong>{template.name}</strong>
                {template.description && <span>{template.description}</span>}
              </button>
            ))}
          </div>
        )}
      </div>
      <div className="builder-grid">
        <label>
          {t('ruleId')}
          <input
            value={draft.id}
            onChange={(event) => setDraft((rule) => ({ ...rule, id: event.target.value }))}
          />
        </label>
        <label>
          {t('ruleName')}
          <input
            value={draft.name}
            onChange={(event) => setDraft((rule) => ({ ...rule, name: event.target.value }))}
          />
        </label>
        <label>
          {t('ruleNameEn')}
          <input
            value={draft.nameEn ?? ''}
            onChange={(event) => setDraft((rule) => ({ ...rule, nameEn: event.target.value }))}
          />
        </label>
        <label>
          {t('ruleNameTr')}
          <input
            value={draft.nameTr ?? ''}
            onChange={(event) => setDraft((rule) => ({ ...rule, nameTr: event.target.value }))}
          />
        </label>
        <label>
          {t('category')}
          <select
            value={draft.category}
            onChange={(event) => setDraft((rule) => ({ ...rule, category: event.target.value }))}
          >
            {ruleCategories().map((item) => (
              <option key={item}>{item}</option>
            ))}
          </select>
        </label>
        <label>
          {t('severity')}
          <select
            value={draft.severity}
            onChange={(event) => setDraft((rule) => ({ ...rule, severity: event.target.value }))}
          >
            {ruleSeverities().map((item) => (
              <option key={item}>{item}</option>
            ))}
          </select>
        </label>
        <label>
          {t('scope')}
          <select
            value={draft.scope}
            onChange={(event) => setDraft((rule) => ({ ...rule, scope: event.target.value }))}
          >
            {ruleScopes().map((item) => (
              <option key={item}>{item}</option>
            ))}
          </select>
        </label>
        <label>
          {t('matchMode')}
          <select
            value={draft.matchMode}
            onChange={(event) => setDraft((rule) => ({ ...rule, matchMode: event.target.value }))}
          >
            <option>All</option>
            <option>Any</option>
          </select>
        </label>
        <label>
          {t('weight')}
          <input
            type="number"
            min="0"
            value={draft.weight}
            onChange={(event) =>
              setDraft((rule) => ({ ...rule, weight: Number(event.target.value) }))
            }
          />
        </label>
        <label>
          {t('maxPenalty')}
          <input
            type="number"
            min="0"
            value={draft.maxPenalty}
            onChange={(event) =>
              setDraft((rule) => ({ ...rule, maxPenalty: Number(event.target.value) }))
            }
          />
        </label>
        <label className="wide-field">
          {t('description')}
          <textarea
            value={draft.description ?? ''}
            onChange={(event) =>
              setDraft((rule) => ({ ...rule, description: event.target.value }))
            }
          />
        </label>
        <label className="wide-field">
          {t('descriptionEn')}
          <textarea
            value={draft.descriptionEn ?? ''}
            onChange={(event) =>
              setDraft((rule) => ({ ...rule, descriptionEn: event.target.value }))
            }
          />
        </label>
        <label className="wide-field">
          {t('descriptionTr')}
          <textarea
            value={draft.descriptionTr ?? ''}
            onChange={(event) =>
              setDraft((rule) => ({ ...rule, descriptionTr: event.target.value }))
            }
          />
        </label>
        <label className="wide-field">
          {t('recommendation')}
          <textarea
            value={draft.recommendation ?? ''}
            onChange={(event) =>
              setDraft((rule) => ({ ...rule, recommendation: event.target.value }))
            }
          />
        </label>
        <label className="wide-field">
          {t('recommendationEn')}
          <textarea
            value={draft.recommendationEn ?? ''}
            onChange={(event) =>
              setDraft((rule) => ({ ...rule, recommendationEn: event.target.value }))
            }
          />
        </label>
        <label className="wide-field">
          {t('recommendationTr')}
          <textarea
            value={draft.recommendationTr ?? ''}
            onChange={(event) =>
              setDraft((rule) => ({ ...rule, recommendationTr: event.target.value }))
            }
          />
        </label>
      </div>
      <div className="conditions-list">
        <h3>{t('conditions')}</h3>
        {draft.conditions.map((condition, index) => (
          <div className="condition-row" key={index}>
            <select
              value={condition.field}
              onChange={(event) =>
                updateCondition(setDraft, index, { field: event.target.value })
              }
            >
              {conditionFields().map((item) => (
                <option key={item}>{item}</option>
              ))}
            </select>
            <select
              value={condition.operator}
              onChange={(event) =>
                updateCondition(setDraft, index, { operator: event.target.value })
              }
            >
              {conditionOperators().map((item) => (
                <option key={item}>{item}</option>
              ))}
            </select>
            <input
              value={condition.propertyName ?? ''}
              onChange={(event) =>
                updateCondition(setDraft, index, { propertyName: event.target.value })
              }
              placeholder={t('propertyName')}
              disabled={condition.field !== 'Activity.Property'}
            />
            <input
              value={condition.value ?? ''}
              onChange={(event) =>
                updateCondition(setDraft, index, { value: event.target.value })
              }
              placeholder={t('value')}
            />
            <input
              value={condition.compareValue ?? ''}
              onChange={(event) =>
                updateCondition(setDraft, index, { compareValue: event.target.value })
              }
              placeholder={t('compareValue')}
              disabled={condition.field !== 'Activity.Property'}
            />
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={Boolean(condition.caseSensitive)}
                onChange={(event) =>
                  updateCondition(setDraft, index, { caseSensitive: event.target.checked })
                }
              />
              {t('caseSensitive')}
            </label>
            <button type="button" onClick={() => removeCondition(setDraft, index)}>
              {t('remove')}
            </button>
          </div>
        ))}
        <button
          type="button"
          onClick={() =>
            setDraft((rule) => ({
              ...rule,
              conditions: [
                ...rule.conditions,
                {
                  field: 'Activity.Name',
                  operator: 'Equals',
                  value: '',
                  caseSensitive: false,
                },
              ],
            }))
          }
        >
          {t('addCondition')}
        </button>
      </div>
      {testResult && (
        <div className="test-result">
          <h3>{t('testRuleResult')}</h3>
          <p>
            {t('customRuleMatches', {
              activities: testResult.matchedActivityCount,
              workflows: testResult.matchedWorkflowCount,
              findings: testResult.estimatedFindingCount,
            })}
          </p>
          {testResult.hasNoiseWarning && <p className="notice">{testResult.noiseWarning}</p>}
          {(testResult.matchedWorkflows ?? []).slice(0, 10).map((workflow) => (
            <span className="workflow-chip" key={workflow}>
              {workflow}
            </span>
          ))}
        </div>
      )}
    </section>
  );
}

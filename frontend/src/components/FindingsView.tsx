import React from 'react';
import { Wrench } from 'lucide-react';
import type { AffectedActivity, Finding, FixApplyResult, FixSuggestion, FixSuggestionResult } from '../services/reportViewModel';
import { getTopIssues } from '../services/reportViewModel';
import { localizeFinding, translate, type Locale } from '../localization';
import { Metric, localizeCategoryLabel } from './uiUtils';

export function Findings({
  findings,
  allFindings,
  severityFilter,
  categoryFilter,
  ruleFilter,
  query,
  categories,
  rules,
  onSeverityFilterChange,
  onCategoryFilterChange,
  onRuleFilterChange,
  onQueryChange,
  fixResult,
  applyResult,
  selectedFixFinding,
  loadingKey,
  isApplyingFix,
  locale,
  t,
  onFix,
  onApply,
  onApplyAll,
  onRenameWorkflow,
}: {
  findings: ReturnType<typeof getTopIssues>;
  allFindings: Finding[];
  severityFilter: string;
  categoryFilter: string;
  ruleFilter: string;
  query: string;
  categories: string[];
  rules: string[];
  onSeverityFilterChange: (value: string) => void;
  onCategoryFilterChange: (value: string) => void;
  onRuleFilterChange: (value: string) => void;
  onQueryChange: (value: string) => void;
  fixResult?: FixSuggestionResult | null;
  applyResult?: FixApplyResult | null;
  selectedFixFinding?: Finding | null;
  loadingKey?: string | null;
  isApplyingFix?: boolean;
  locale: Locale;
  t: (key: string, values?: Record<string, unknown>) => string;
  onFix?: (finding: Finding, useAi?: boolean) => void;
  onApply?: (suggestion: FixSuggestion) => void;
  onApplyAll?: () => void;
  onRenameWorkflow?: (suggestion: FixSuggestion, newWorkflowPath: string) => void;
}) {
  const pageSize = 100;
  const [visibleCount, setVisibleCount] = React.useState(pageSize);
  const [sortOrder, setSortOrder] = React.useState('default');

  React.useEffect(() => {
    setVisibleCount(pageSize);
  }, [categoryFilter, query, ruleFilter, severityFilter, sortOrder]);

  const orderedFindings = React.useMemo(() => {
    if (sortOrder === 'default') {
      return findings;
    }

    const severityRank: Record<string, number> = {
      Critical: 5,
      Error: 4,
      Warning: 3,
      Suggestion: 2,
      Info: 1,
    };
    const direction = sortOrder === 'severityAsc' ? 1 : -1;
    return [...findings].sort((left, right) =>
      direction * ((severityRank[left.severity] ?? 0) - (severityRank[right.severity] ?? 0)));
  }, [findings, sortOrder]);

  const visibleFindings = orderedFindings.slice(0, visibleCount);
  const autoFixableOccurrenceCount = allFindings
    .filter((finding) => finding.ruleId === 'RPA007')
    .reduce((total, finding) => total + Math.max(1, finding.affectedActivityCount ?? finding.occurrenceCount ?? 1), 0);

  return (
    <div className="findings-view">
      <div className="filter-bar">
        <div className="search-input-wrapper">
          <input
            aria-label={t('searchFindings')}
            placeholder={t('searchFindings')}
            value={query}
            onChange={(event) => onQueryChange(event.target.value)}
          />
          {query && (
            <button
              type="button"
              className="clear-input-btn"
              aria-label={t('clearFilters')}
              onClick={() => onQueryChange('')}
            >
              ✕
            </button>
          )}
        </div>
        {['All', 'Critical', 'Error', 'Warning', 'Suggestion', 'Info'].map((severity) => (
          <button key={severity} type="button" className={severityFilter === severity ? 'active' : ''} onClick={() => onSeverityFilterChange(severity)}>
            {severity === 'All' ? t('all') : severity}
          </button>
        ))}
        <select aria-label={t('ruleId')} value={ruleFilter} onChange={(event) => onRuleFilterChange(event.target.value)}>
          {rules.map((rule) => <option key={rule} value={rule}>{rule === 'All' ? t('allRules') : rule}</option>)}
        </select>
        <select aria-label={t('findingCategory')} value={categoryFilter} onChange={(event) => onCategoryFilterChange(event.target.value)}>
          {categories.map((category) => (
            <option key={category} value={category}>{category === 'All' ? t('allCategories') : localizeCategoryLabel(category, t)}</option>
          ))}
        </select>
        <select aria-label={t('findingSort')} value={sortOrder} onChange={(event) => setSortOrder(event.target.value)}>
          <option value="default">{t('defaultOrder')}</option>
          <option value="severityDesc">{t('severityHighToLow')}</option>
          <option value="severityAsc">{t('severityLowToHigh')}</option>
        </select>
        {(query || severityFilter !== 'All' || ruleFilter !== 'All' || categoryFilter !== 'All') && (
          <button
            type="button"
            className="reset-filters-btn"
            onClick={() => {
              onQueryChange('');
              onSeverityFilterChange('All');
              onRuleFilterChange('All');
              onCategoryFilterChange('All');
            }}
          >
            {t('clearFilters')}
          </button>
        )}
        <span className="hint">{t('showingFindings', { shown: findings.length, total: allFindings.length })}</span>
        {autoFixableOccurrenceCount > 0 && onApplyAll && (
          <button type="button" className="primary-action" onClick={onApplyAll} disabled={isApplyingFix}>
            <Wrench size={16} />
            {t('applyAllSafeFixes', { count: autoFixableOccurrenceCount })}
          </button>
        )}
      </div>
      {fixResult && (
        <FixSuggestionPanel
          result={fixResult}
          applyResult={applyResult}
          isApplyingFix={isApplyingFix}
          t={t}
          onApply={onApply}
          onRenameWorkflow={onRenameWorkflow}
          onGenerateAi={onFix && selectedFixFinding ? () => onFix(selectedFixFinding, true) : undefined}
        />
      )}
      {findings.length === 0 ? <p className="empty-state">{t('noFilteredFindings')}</p> : visibleFindings.map((finding) => {
        const key = `${finding.ruleId}-${finding.workflowPath ?? ''}-${finding.activityId ?? ''}-false`;
        return (
          <FindingRow
            key={finding.id ?? `${finding.ruleId}-${finding.workflowPath}-${finding.activityId ?? finding.activityDisplayName}-${finding.message}`}
            finding={finding}
            locale={locale}
            t={t}
            onFix={onFix}
            isFixLoading={loadingKey === key}
          />
        );
      })}
      {visibleCount < findings.length && (
        <div className="incremental-list-action">
          <button type="button" onClick={() => setVisibleCount((count) => Math.min(count + pageSize, findings.length))}>
            {t('showMoreFindings', { remaining: findings.length - visibleCount })}
          </button>
        </div>
      )}
    </div>
  );
}

export function ApplyAllFixesDialog({ count, isApplying, t, onCancel, onConfirm }: {
  count: number;
  isApplying: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div className="modal-backdrop" role="presentation">
      <section className="confirmation-dialog" role="dialog" aria-modal="true" aria-labelledby="apply-all-fixes-title">
        <h2 id="apply-all-fixes-title">{t('applyAllSafeFixesTitle')}</h2>
        <p>{t('applyAllSafeFixesDescription', { count })}</p>
        <p className="notice">{t('applyAllSafeFixesSafety')}</p>
        <div className="dialog-actions">
          <button type="button" onClick={onCancel} disabled={isApplying}>{t('cancel')}</button>
          <button className="primary-action" type="button" onClick={onConfirm} disabled={isApplying}>
            {isApplying ? t('applying') : t('applyAllSafeFixesConfirm')}
          </button>
        </div>
      </section>
    </div>
  );
}

export function FindingRow({
  finding,
  locale = 'en',
  t = (key, values) => translate('en', key, values),
  onFix,
  isFixLoading = false,
}: {
  finding: ReturnType<typeof getTopIssues>[number];
  locale?: Locale;
  t?: (key: string, values?: Record<string, unknown>) => string;
  onFix?: (finding: Finding, useAi?: boolean) => void;
  isFixLoading?: boolean;
}) {
  const displayFinding = localizeFinding(finding, locale);
  const isAggregated = finding.scope === 'Aggregated' || (finding.affectedActivityCount ?? 0) > 0;
  const examples = (finding.affectedActivities?.length ? finding.affectedActivities : finding.exampleActivities) ?? [];
  return (
    <article className="finding-row">
      <strong>{displayFinding.severity} · {displayFinding.ruleId} {displayFinding.ruleName}</strong>
      <p>{displayFinding.message}</p>
      {displayFinding.recommendation && <p className="hint">{displayFinding.recommendation}</p>}
      <span>{displayFinding.workflowPath ?? 'Project'}{displayFinding.activityDisplayName ? ` · ${displayFinding.activityDisplayName}` : ''}</span>
      {isAggregated && (
        <div className="aggregation-summary">
          <span>{finding.affectedActivityCount ?? finding.occurrenceCount ?? 0} {t('affectedActivities')}</span>
          {typeof finding.totalRelevantActivityCount === 'number' && <span>{finding.totalRelevantActivityCount} {t('relevantActivities')}</span>}
          {typeof finding.percentage === 'number' && <span>{finding.percentage.toFixed(1)}%</span>}
          {examples.length > 0 && (
            <ul>
              {examples.slice(0, 10).map((activity) => (
                <li key={activity.activityId ?? `${activity.activityName}-${activity.activityPath}`}>
                  <span>{activity.activityName} · {activity.activityDisplayName}</span>
                  {onFix && (
                    <button
                      type="button"
                      className="inline-action"
                      onClick={() => onFix(findingForAffectedActivity(finding, activity))}
                    >
                      {t('fixSuggestion')}
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
      {onFix && (
        <div className="row-actions">
          <button type="button" onClick={() => onFix(finding)} disabled={isFixLoading}>
            <Wrench size={16} />
            {isFixLoading ? t('generating') : t('fixSuggestion')}
          </button>
        </div>
      )}
    </article>
  );
}

export function findingForAffectedActivity(finding: Finding, activity: AffectedActivity): Finding {
  return {
    ...finding,
    activityId: activity.activityId ?? activity.stableId ?? activity.activityPath ?? finding.activityId,
    activityName: activity.activityName,
    activityDisplayName: activity.activityDisplayName,
    propertyName: activity.propertyName ?? finding.propertyName ?? 'DisplayName',
    currentValue: activity.currentValue ?? finding.currentValue ?? activity.activityDisplayName,
  };
}

export function FixSuggestionPanel({
  result,
  applyResult,
  isApplyingFix = false,
  t,
  onApply,
  onGenerateAi,
  onRenameWorkflow,
}: {
  result: FixSuggestionResult;
  applyResult?: FixApplyResult | null;
  isApplyingFix?: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
  onApply?: (suggestion: FixSuggestion) => void;
  onGenerateAi?: () => void;
  onRenameWorkflow?: (suggestion: FixSuggestion, newWorkflowPath: string) => void;
}) {
  const suggestion = result.suggestion;
  const [renamePath, setRenamePath] = React.useState('');
  React.useEffect(() => {
    setRenamePath(suggestion?.ruleId === 'RPA006' ? suggestion.suggestedValue ?? '' : '');
  }, [suggestion?.id, suggestion?.ruleId, suggestion?.suggestedValue]);
  if (!suggestion) {
    return (
      <section className="fix-panel">
        <h2>{t('suggestedFix')}</h2>
        <p>{result.message ?? t('noFixAvailable')}</p>
        {onGenerateAi && (
          <>
            <p className="notice">{t('aiFixPrivacy')}</p>
            <button type="button" onClick={onGenerateAi}>{t('generateAiSuggestion')}</button>
          </>
        )}
      </section>
    );
  }

  return (
    <section className="fix-panel">
      <h2>{t('suggestedFix')}</h2>
      <div className="metric-grid">
        <Metric label={t('rule')} value={suggestion.ruleId} />
        <Metric label={t('fixType')} value={suggestion.fixType} />
        <Metric label={t('fixability')} value={suggestion.fixability ?? (suggestion.canAutoApply ? 'SafeAutomatic' : 'Previewable')} />
        <Metric label={t('risk')} value={suggestion.riskLevel} />
        <Metric label={t('confidence')} value={suggestion.confidence} />
        <Metric label={t('aiAssisted')} value={suggestion.requiresAi ? t('yes') : t('no')} />
        <Metric label={t('autoApply')} value={suggestion.canAutoApply ? t('available') : t('previewOnly')} />
      </div>
      {suggestion.confidence === 'Low' && <p className="notice">{t('reviewLowConfidenceFix')}</p>}
      {suggestion.riskLevel === 'High' && <p className="notice high-risk">{t('highRiskFix')}</p>}
      {suggestion.requiresAi && <p className="notice">{t('aiSuggestionManualReview')}</p>}
      <h3>{suggestion.title}</h3>
      <p>{suggestion.description}</p>
      <p><strong>{t('why')}</strong> {suggestion.explanation}</p>
      <div className="preview-grid">
        <div>
          <strong>{t('before')}</strong>
          <pre>{suggestion.patchPreview?.before ?? suggestion.beforePreview ?? t('noBeforePreview')}</pre>
        </div>
        <div>
          <strong>{t('after')}</strong>
          <pre>{suggestion.patchPreview?.after ?? suggestion.afterPreview ?? t('noAfterPreview')}</pre>
        </div>
      </div>
      {(suggestion.steps ?? []).length > 0 && (
        <section>
          <h3>{t('steps')}</h3>
          <ul>{suggestion.steps!.map((step) => <li key={step}>{step}</li>)}</ul>
        </section>
      )}
      {(suggestion.risks ?? []).length > 0 && (
        <section>
          <h3>{t('risks')}</h3>
          <ul>{suggestion.risks!.map((risk) => <li key={risk}>{risk}</li>)}</ul>
        </section>
      )}
      {(suggestion.userInputHints ?? []).length > 0 && (
        <section>
          <h3>{t('userInputNeeded')}</h3>
          <ul>{suggestion.userInputHints!.map((hint) => <li key={hint}>{hint}</li>)}</ul>
        </section>
      )}
      {(suggestion.validationNotes ?? []).length > 0 && (
        <section>
          <h3>{t('validationNotes')}</h3>
          <ul>{suggestion.validationNotes!.map((note) => <li key={note}>{note}</li>)}</ul>
        </section>
      )}
      <button type="button" onClick={() => copyFixInstructions(suggestion)}>{t('copyFixInstructions')}</button>
      {suggestion.ruleId === 'RPA006' && !suggestion.requiresAi && onRenameWorkflow && (
        <section className="workflow-rename-control">
          <label htmlFor="workflow-rename-path">{t('newWorkflowPath')}</label>
          <input
            id="workflow-rename-path"
            value={renamePath}
            onChange={(event) => setRenamePath(event.target.value)}
            placeholder="Business/DescriptiveWorkflowName.xaml"
          />
          <p className="hint">{t('workflowRenameReferencesHelp')}</p>
          <button
            className="primary-action"
            type="button"
            disabled={isApplyingFix || !renamePath.trim() || renamePath.trim() === suggestion.workflowPath}
            onClick={() => onRenameWorkflow(suggestion, renamePath.trim())}
          >
            {t('renameWorkflow')}
          </button>
        </section>
      )}
      {result.validation && !result.validation.isValid && (
        <p className="error-text">{result.message ?? t('fixSuggestionStale')}</p>
      )}
      {suggestion.canAutoApply && !suggestion.requiresAi ? (
        <button className="primary-action" type="button" onClick={() => onApply?.(suggestion)} disabled={isApplyingFix}>
          <Wrench size={16} />
          {isApplyingFix ? t('applying') : t('applyFix')}
        </button>
      ) : (
        <p className="notice">{t('manualChangeRequired')}</p>
      )}
      {applyResult && (
        <section className={applyResult.success ? 'apply-result success' : 'apply-result error'}>
          <h3>{applyResult.success ? t('fixAppliedSuccessfully') : t('fixWasNotApplied')}</h3>
          <p>{applyResult.message}</p>
          {applyResult.workflowPath && <p>Workflow: {applyResult.workflowPath}</p>}
          {applyResult.previousValue && <p>{t('before')}: {applyResult.previousValue}</p>}
          {applyResult.newValue && <p>{t('after')}: {applyResult.newValue}</p>}
          {applyResult.backupPath && <p>{t('backupCreated')}: {applyResult.backupPath}</p>}
          {applyResult.requiresReanalysis && <p>{t('projectFilesChangedReanalysis')}</p>}
        </section>
      )}
      {!suggestion.requiresAi && onGenerateAi && (
        <>
          <p className="notice">{t('aiFixPrivacy')}</p>
          <button type="button" onClick={onGenerateAi}>{t('generateAiSuggestion')}</button>
        </>
      )}
    </section>
  );
}

export function RenameWorkflowDialog({ suggestion, newWorkflowPath, isApplying, t, onCancel, onConfirm }: {
  suggestion: FixSuggestion;
  newWorkflowPath: string;
  isApplying: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div className="modal-backdrop" role="presentation">
      <section className="confirmation-dialog" role="dialog" aria-modal="true" aria-labelledby="rename-workflow-title">
        <h2 id="rename-workflow-title">{t('renameWorkflowConfirmTitle')}</h2>
        <div className="preview-grid">
          <div><strong>{t('before')}</strong><pre>{suggestion.workflowPath}</pre></div>
          <div><strong>{t('after')}</strong><pre>{newWorkflowPath}</pre></div>
        </div>
        <p className="notice">{t('renameWorkflowSafety')}</p>
        <div className="dialog-actions">
          <button type="button" onClick={onCancel} disabled={isApplying}>{t('cancel')}</button>
          <button className="primary-action" type="button" onClick={onConfirm} disabled={isApplying}>
            {isApplying ? t('applying') : t('renameWorkflow')}
          </button>
        </div>
      </section>
    </div>
  );
}

export function copyFixInstructions(suggestion: FixSuggestion) {
  const sections = [
    `Fix: ${suggestion.title}`,
    `Rule: ${suggestion.ruleId}`,
    `Fixability: ${suggestion.fixability ?? (suggestion.canAutoApply ? 'SafeAutomatic' : 'Previewable')}`,
    `Confidence: ${suggestion.confidence}`,
    `Workflow: ${suggestion.workflowPath ?? 'Project'}`,
    suggestion.activityDisplayName ? `Activity: ${suggestion.activityDisplayName}` : null,
    '',
    'Why:',
    suggestion.explanation,
    '',
    'Current:',
    suggestion.patchPreview?.before ?? suggestion.beforePreview ?? suggestion.currentState ?? 'n/a',
    '',
    'Proposed:',
    suggestion.patchPreview?.after ?? suggestion.afterPreview ?? suggestion.proposedState ?? 'n/a',
    '',
    ...(suggestion.steps?.length ? ['Steps:', ...suggestion.steps.map((step) => `- ${step}`), ''] : []),
    ...(suggestion.risks?.length ? ['Risks:', ...suggestion.risks.map((risk) => `- ${risk}`), ''] : []),
  ].filter((line): line is string => line !== null);

  void navigator.clipboard?.writeText(sections.join('\n'));
}

export function ApplyFixDialog({
  suggestion,
  isApplying,
  t,
  onCancel,
  onConfirm,
}: {
  suggestion: FixSuggestion;
  isApplying: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div className="modal-backdrop" role="presentation">
      <section className="confirmation-dialog" role="dialog" aria-modal="true" aria-labelledby="apply-fix-title">
        <h2 id="apply-fix-title">{t('applyThisChange')}</h2>
        <p>Workflow: {suggestion.workflowPath}</p>
        <p>{t('property')}: {suggestion.propertyName}</p>
        <div className="preview-grid">
          <div>
            <strong>{t('before')}</strong>
            <pre>{suggestion.currentValue ?? suggestion.beforePreview ?? t('unknown')}</pre>
          </div>
          <div>
            <strong>{t('after')}</strong>
            <pre>{suggestion.suggestedValue ?? suggestion.afterPreview ?? t('unknown')}</pre>
          </div>
        </div>
        <p className="notice">{t('backupWillBeCreated')}</p>
        <div className="dialog-actions">
          <button type="button" onClick={onCancel} disabled={isApplying}>{t('cancel')}</button>
          <button className="primary-action" type="button" onClick={onConfirm} disabled={isApplying}>
            {isApplying ? t('applying') : t('applyFix')}
          </button>
        </div>
      </section>
    </div>
  );
}

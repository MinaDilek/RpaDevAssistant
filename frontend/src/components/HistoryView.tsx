import React from 'react';
import { History, RotateCcw } from 'lucide-react';
import type {
  AnalysisComparison,
  AnalysisSnapshotSummary,
  BackupSummary,
  UndoResult,
} from '../services/reportViewModel';
import { Metric } from './uiUtils';

export function ChangeHistory({
  backups,
  snapshots,
  currentProjectPath,
  selectedComparison,
  comparisonLoading,
  isLoading,
  undoResult,
  t,
  onRefresh,
  onCompare,
  onUndo,
}: {
  backups: BackupSummary[];
  snapshots: AnalysisSnapshotSummary[];
  currentProjectPath: string;
  selectedComparison?: AnalysisComparison | null;
  comparisonLoading: boolean;
  isLoading: boolean;
  undoResult?: UndoResult | null;
  t: (key: string, values?: Record<string, unknown>) => string;
  onRefresh: () => void;
  onCompare: (snapshot: AnalysisSnapshotSummary) => void;
  onUndo: (backup: BackupSummary) => void;
}) {
  const [projectFilter, setProjectFilter] = React.useState(() =>
    currentProjectPath ? normalizeHistoryProjectPath(currentProjectPath) : 'All',
  );
  React.useEffect(() => {
    if (currentProjectPath) {
      setProjectFilter(normalizeHistoryProjectPath(currentProjectPath));
    }
  }, [currentProjectPath]);

  const projectOptions = React.useMemo(() => {
    const options = new Map<string, string>();
    snapshots.forEach((snapshot) => {
      if (snapshot.projectPath) {
        options.set(
          normalizeHistoryProjectPath(snapshot.projectPath),
          snapshot.projectName
            ? `${snapshot.projectName} · ${snapshot.projectPath}`
            : snapshot.projectPath,
        );
      }
    });
    return Array.from(options.entries()).map(([value, label]) => ({ value, label }));
  }, [snapshots]);
  const visibleSnapshots =
    projectFilter === 'All'
      ? snapshots
      : snapshots.filter(
          (snapshot) =>
            snapshot.projectPath &&
            normalizeHistoryProjectPath(snapshot.projectPath) === projectFilter,
        );

  return (
    <div className="history-view">
      <div className="history-header">
        <h2>{t('changeHistory')}</h2>
        <button type="button" onClick={onRefresh} disabled={isLoading}>
          <History size={16} />
          {isLoading ? t('loading') : t('refresh')}
        </button>
      </div>
      <section className="history-section">
        <h3>{t('analysisHistory')}</h3>
        <div className="filter-row">
          <label>
            {t('project')}
            <select
              aria-label={t('historyProjectFilter')}
              value={projectFilter}
              onChange={(event) => setProjectFilter(event.target.value)}
            >
              <option value="All">{t('allProjects')}</option>
              {projectOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
        </div>
        {visibleSnapshots.length === 0 ? (
          <p className="empty-state">{t('noAnalysisHistory')}</p>
        ) : (
          <div className="history-list">
            <p className="hint">
              {projectFilter === 'All' ? t('allProjectsHistory') : t('projectHistoryOnly')}
            </p>
            {visibleSnapshots.map((snapshot) => {
              const previousSnapshotId =
                snapshot.previousSnapshotId ?? findPreviousSnapshotId(snapshot, visibleSnapshots);
              return (
                <article className="history-entry" key={snapshot.snapshotId}>
                  <div>
                    <strong>{formatTimestamp(snapshot.generatedAtUtc)}</strong>
                    <p>
                      {snapshot.projectName ?? '-'} · {snapshot.projectPath ?? '-'}
                    </p>
                    <p>
                      {t('score')}: {snapshot.score} · {t('grade')} {snapshot.grade} ·{' '}
                      {snapshot.totalFindings} {t('findingsNav')}
                    </p>
                    <p>
                      {snapshot.workflowCount} {t('workflows')} · {snapshot.totalActivityCount}{' '}
                      {t('activities')}
                    </p>
                    {snapshot.scoreDelta !== null && snapshot.scoreDelta !== undefined && (
                      <span>
                        {t('scoreChange')}: {formatSigned(snapshot.scoreDelta)} · {t('newFindings')}:{' '}
                        {snapshot.newFindingCount ?? 0} · {t('resolvedFindings')}:{' '}
                        {snapshot.resolvedFindingCount ?? 0}
                      </span>
                    )}
                  </div>
                  <div className="history-actions">
                    {previousSnapshotId ? (
                      <button
                        type="button"
                        onClick={() => onCompare({ ...snapshot, previousSnapshotId })}
                        disabled={comparisonLoading}
                      >
                        {comparisonLoading ? t('loading') : t('comparePrevious')}
                      </button>
                    ) : (
                      <span className="hint">{t('noPreviousSnapshot')}</span>
                    )}
                  </div>
                </article>
              );
            })}
          </div>
        )}
      </section>
      {selectedComparison && (
        <section className="comparison-panel">
          <h3>{t('beforeAfterComparison')}</h3>
          <div className="metric-grid compact">
            <Metric label={t('scoreChange')} value={formatSigned(selectedComparison.scoreDelta)} />
            <Metric
              label={t('findingChange')}
              value={formatSigned(selectedComparison.totalFindingDelta)}
            />
            <Metric label={t('newFindings')} value={selectedComparison.newFindings.length} />
            <Metric
              label={t('resolvedFindings')}
              value={selectedComparison.resolvedFindings.length}
            />
            <Metric
              label={t('unchangedFindings')}
              value={selectedComparison.unchangedFindings.length}
            />
            <Metric label={t('changedFindings')} value={selectedComparison.changedFindings.length} />
          </div>
          <ComparisonFindingList
            title={t('newFindings')}
            findings={selectedComparison.newFindings}
            emptyLabel={t('none')}
          />
          <ComparisonFindingList
            title={t('resolvedFindings')}
            findings={selectedComparison.resolvedFindings}
            emptyLabel={t('none')}
          />
          <h4>{t('workflowChanges')}</h4>
          {selectedComparison.workflowChanges.length === 0 ? (
            <p className="empty-state">{t('noWorkflowChanges')}</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Workflow</th>
                  <th>{t('findingChange')}</th>
                  <th>{t('activityChange')}</th>
                  <th>{t('complexityChange')}</th>
                </tr>
              </thead>
              <tbody>
                {selectedComparison.workflowChanges.slice(0, 10).map((workflow) => (
                  <tr key={workflow.workflowPath}>
                    <td>{workflow.workflowPath}</td>
                    <td>{formatSigned(workflow.findingCountDelta)}</td>
                    <td>{formatSigned(workflow.activityCountDelta)}</td>
                    <td>
                      {workflow.complexityScoreDelta === null ||
                      workflow.complexityScoreDelta === undefined
                        ? '-'
                        : formatSigned(workflow.complexityScoreDelta)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      )}
      {undoResult && (
        <section className={undoResult.success ? 'apply-result success' : 'apply-result error'}>
          <h3>
            {undoResult.success ? t('changeRestoredSuccessfully') : t('changeWasNotRestored')}
          </h3>
          <p>{undoResult.message}</p>
          {undoResult.workflowPath && <p>Workflow: {undoResult.workflowPath}</p>}
          {undoResult.safetyBackupId && (
            <p>
              {t('safetyBackupCreated')}: {undoResult.safetyBackupId}
            </p>
          )}
          {undoResult.requiresReanalysis && <p>{t('reanalysisRequired')}</p>}
        </section>
      )}
      <section className="history-section">
        <h3>{t('fixHistory')}</h3>
        {backups.length === 0 ? (
          <p className="empty-state">{t('noFixHistory')}</p>
        ) : (
          <div className="history-list">
            {backups.map((backup) => (
              <article className="history-entry" key={backup.backupId}>
                <div>
                  <strong>{backup.workflowPath ?? t('unknownWorkflow')}</strong>
                  <p>
                    {backup.ruleId ?? t('unknownRule')} · {backup.propertyName ?? t('property')}
                  </p>
                  <p>
                    {backup.previousValue ?? 'Before'} → {backup.newValue ?? 'After'}
                  </p>
                  <span>
                    {t('applied')}: {formatTimestamp(backup.createdAtUtc)}
                  </span>
                </div>
                <div className="history-actions">
                  <span className={`history-status ${backup.status.toLowerCase()}`}>
                    {backup.status}
                  </span>
                  {backup.canUndo ? (
                    <button type="button" onClick={() => onUndo(backup)}>
                      <RotateCcw size={16} />
                      {t('undo')}
                    </button>
                  ) : (
                    <span className="hint">{backup.reason ?? t('undoUnavailable')}</span>
                  )}
                </div>
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

export function ComparisonFindingList({
  title,
  findings,
  emptyLabel,
}: {
  title: string;
  findings: AnalysisComparison['newFindings'];
  emptyLabel: string;
}) {
  return (
    <div className="comparison-findings">
      <h4>{title}</h4>
      {findings.length === 0 ? (
        <p className="empty-state">{emptyLabel}</p>
      ) : (
        <ul>
          {findings.slice(0, 10).map((item) => (
            <li
              key={`${item.state}-${item.finding.id}-${item.finding.contentHash ?? item.finding.message}`}
            >
              <strong>{item.finding.ruleId}</strong> {item.finding.workflowPath ?? ''} ·{' '}
              {item.finding.message ?? item.finding.ruleName}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export function UndoDialog({
  backup,
  isUndoing,
  t,
  onCancel,
  onConfirm,
}: {
  backup: BackupSummary;
  isUndoing: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div className="modal-backdrop" role="presentation">
      <section
        className="confirmation-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="undo-fix-title"
      >
        <h2 id="undo-fix-title">{t('undoThisChange')}</h2>
        <p>Workflow: {backup.workflowPath}</p>
        <p>
          {t('current')}: {backup.newValue}
        </p>
        <p>
          {t('restore')}: {backup.previousValue}
        </p>
        <p className="notice">{t('backupWillBeCreated')}</p>
        <div className="dialog-actions">
          <button type="button" onClick={onCancel} disabled={isUndoing}>
            {t('cancel')}
          </button>
          <button
            className="primary-action"
            type="button"
            onClick={onConfirm}
            disabled={isUndoing}
          >
            {isUndoing ? t('undoing') : t('undoChange')}
          </button>
        </div>
      </section>
    </div>
  );
}

function formatSigned(value: number): string {
  return value > 0 ? `+${value}` : value.toString();
}

function formatTimestamp(value?: string | null): string {
  if (!value) {
    return 'Unknown';
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

export function findPreviousSnapshotId(
  snapshot: AnalysisSnapshotSummary,
  snapshots: AnalysisSnapshotSummary[],
): string | null {
  const currentTime = Date.parse(snapshot.generatedAtUtc ?? '');
  const sameProjectSnapshots = snapshots
    .filter(
      (candidate) =>
        candidate.snapshotId !== snapshot.snapshotId &&
        sameProjectPath(candidate.projectPath, snapshot.projectPath),
    )
    .sort(
      (left, right) =>
        Date.parse(right.generatedAtUtc ?? '') - Date.parse(left.generatedAtUtc ?? ''),
    );
  const previous = Number.isNaN(currentTime)
    ? sameProjectSnapshots[0]
    : sameProjectSnapshots.find((candidate) => Date.parse(candidate.generatedAtUtc ?? '') < currentTime) ??
      sameProjectSnapshots[0];

  return previous?.snapshotId ?? null;
}

function sameProjectPath(left?: string | null, right?: string | null): boolean {
  if (!left || !right) {
    return false;
  }

  return normalizeHistoryProjectPath(left) === normalizeHistoryProjectPath(right);
}

function normalizeHistoryProjectPath(value: string): string {
  return value.replaceAll('\\', '/').replace(/\/+$/, '').toLowerCase();
}

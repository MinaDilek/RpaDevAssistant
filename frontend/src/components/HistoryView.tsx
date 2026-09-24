import React from 'react';
import { History, RotateCcw } from 'lucide-react';
import type {
  AnalysisComparison,
  AnalysisSnapshotSummary,
  BackupSummary,
  GitComparisonResult,
  PullRequestReviewResult,
  UndoResult,
} from '../services/reportViewModel';
import { Metric } from './uiUtils';

export function ChangeHistory({
  backups,
  snapshots,
  currentProjectPath,
  selectedComparison,
  gitComparison,
  gitComparisonLoading,
  pullRequestReview,
  pullRequestLoading,
  comparisonLoading,
  isLoading,
  undoResult,
  t,
  onRefresh,
  onCompare,
  onCompareGitRefs,
  onReviewPullRequest,
  onCommentPullRequest,
  onUndo,
}: {
  backups: BackupSummary[];
  snapshots: AnalysisSnapshotSummary[];
  currentProjectPath: string;
  selectedComparison?: AnalysisComparison | null;
  gitComparison?: GitComparisonResult | null;
  gitComparisonLoading: boolean;
  pullRequestReview?: PullRequestReviewResult | null;
  pullRequestLoading: boolean;
  comparisonLoading: boolean;
  isLoading: boolean;
  undoResult?: UndoResult | null;
  t: (key: string, values?: Record<string, unknown>) => string;
  onRefresh: () => void;
  onCompare: (snapshot: AnalysisSnapshotSummary) => void;
  onCompareGitRefs: (baselineRef: string, targetRef: string) => void;
  onReviewPullRequest: (provider: string, repository: string, pullRequestId: number) => void;
  onCommentPullRequest: (review: PullRequestReviewResult) => void;
  onUndo: (backup: BackupSummary) => void;
}) {
  const [baselineRef, setBaselineRef] = React.useState('HEAD~1');
  const [targetRef, setTargetRef] = React.useState('HEAD');
  const [provider, setProvider] = React.useState('GitHub');
  const [repository, setRepository] = React.useState('');
  const [pullRequestId, setPullRequestId] = React.useState('');
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
            {visibleSnapshots.length > 1 && (
              <AnalysisTrendChart snapshots={visibleSnapshots} t={t} />
            )}
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
      <section className="history-section git-comparison-section">
        <h3>{t('gitComparison')}</h3>
        <p className="hint">{t('gitComparisonHint')}</p>
        <div className="filter-row git-ref-fields">
          <label>
            {t('baselineRef')}
            <input value={baselineRef} onChange={(event) => setBaselineRef(event.target.value)} />
          </label>
          <label>
            {t('targetRef')}
            <input value={targetRef} onChange={(event) => setTargetRef(event.target.value)} />
          </label>
          <button
            type="button"
            disabled={gitComparisonLoading || !currentProjectPath || !baselineRef.trim() || !targetRef.trim()}
            onClick={() => onCompareGitRefs(baselineRef.trim(), targetRef.trim())}
          >
            {gitComparisonLoading ? t('loading') : t('compareRefs')}
          </button>
        </div>
        {gitComparison && (
          <div className="comparison-panel git-comparison-result">
            <div className="metric-grid compact">
              <Metric label={t('baselineScore')} value={gitComparison.baselineScore} />
              <Metric label={t('targetScore')} value={gitComparison.targetScore} />
              <Metric label={t('scoreChange')} value={formatSigned(gitComparison.scoreDelta)} />
              <Metric label={t('findingChange')} value={formatSigned(gitComparison.findingDelta)} />
            </div>
            <p className="hint">
              {gitComparison.baselineRef} ({shortCommit(gitComparison.baselineCommit)}) →{' '}
              {gitComparison.targetRef} ({shortCommit(gitComparison.targetCommit)})
            </p>
            <GitFindingList title={t('newFindings')} findings={gitComparison.newFindings} emptyLabel={t('none')} />
            <GitFindingList title={t('resolvedFindings')} findings={gitComparison.resolvedFindings} emptyLabel={t('none')} />
            <h4>{t('changedFiles')}</h4>
            {gitComparison.changedFiles.length === 0 ? (
              <p className="empty-state">{t('none')}</p>
            ) : (
              <ul className="compact-list">
                {gitComparison.changedFiles.map((file) => <li key={file}>{file}</li>)}
              </ul>
            )}
          </div>
        )}
      </section>
      <section className="history-section">
        <h3>{t('pullRequestReview')}</h3>
        <p className="hint">{t('pullRequestReviewHint')}</p>
        <div className="filter-row git-ref-fields">
          <label>{t('provider')}<select value={provider} onChange={(event) => setProvider(event.target.value)}><option>GitHub</option><option>GitLab</option><option>AzureDevOps</option></select></label>
          <label>{t('repository')}<input value={repository} onChange={(event) => setRepository(event.target.value)} placeholder={provider === 'GitHub' ? 'owner/repository' : 'group/repository'} /></label>
          <label>{t('pullRequestId')}<input type="number" min="1" value={pullRequestId} onChange={(event) => setPullRequestId(event.target.value)} /></label>
          <button type="button" disabled={pullRequestLoading || !currentProjectPath || !repository.trim() || Number(pullRequestId) <= 0} onClick={() => onReviewPullRequest(provider, repository.trim(), Number(pullRequestId))}>
            {pullRequestLoading ? t('loading') : t('reviewPullRequest')}
          </button>
        </div>
        {pullRequestReview?.comparison && <div className="comparison-panel">
          <h4>{pullRequestReview.pullRequest?.title ?? `#${pullRequestReview.pullRequestId}`}</h4>
          <div className="metric-grid compact">
            <Metric label={t('scoreChange')} value={formatSigned(pullRequestReview.comparison.scoreDelta)} />
            <Metric label={t('findingChange')} value={formatSigned(pullRequestReview.comparison.findingDelta)} />
            <Metric label={t('newFindings')} value={pullRequestReview.comparison.newFindings.length} />
            <Metric label={t('resolvedFindings')} value={pullRequestReview.comparison.resolvedFindings.length} />
          </div>
          <button type="button" onClick={() => onCommentPullRequest(pullRequestReview)}>{t('publishReviewComment')}</button>
        </div>}
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

function GitFindingList({ title, findings, emptyLabel }: {
  title: string;
  findings: GitComparisonResult['newFindings'];
  emptyLabel: string;
}) {
  return (
    <div>
      <h4>{title}</h4>
      {findings.length === 0 ? <p className="empty-state">{emptyLabel}</p> : (
        <ul className="compact-list">
          {findings.slice(0, 20).map((finding, index) => (
            <li key={finding.id ?? `${finding.ruleId}-${finding.workflowPath}-${index}`}>
              <strong>{finding.ruleId}</strong> · {finding.workflowPath ?? '-'} · {finding.message ?? finding.ruleName ?? '-'}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function shortCommit(value?: string | null): string {
  return value ? value.slice(0, 8) : '-';
}

function AnalysisTrendChart({
  snapshots,
  t,
}: {
  snapshots: AnalysisSnapshotSummary[];
  t: (key: string, values?: Record<string, unknown>) => string;
}) {
  const points = [...snapshots]
    .sort(
      (left, right) =>
        (Date.parse(left.generatedAtUtc ?? '') || 0) -
        (Date.parse(right.generatedAtUtc ?? '') || 0),
    )
    .slice(-12);
  const width = 720;
  const height = 190;
  const padding = 28;
  const chartWidth = width - padding * 2;
  const chartHeight = height - padding * 2;
  const maxFindings = Math.max(1, ...points.map((snapshot) => snapshot.totalFindings));
  const x = (index: number) =>
    padding + (points.length === 1 ? chartWidth / 2 : (index / (points.length - 1)) * chartWidth);
  const scoreY = (score: number) => padding + ((100 - Math.max(0, Math.min(100, score))) / 100) * chartHeight;
  const findingY = (count: number) => padding + (1 - Math.max(0, count) / maxFindings) * chartHeight;
  const scorePoints = points.map((snapshot, index) => `${x(index)},${scoreY(snapshot.score)}`).join(' ');
  const findingPoints = points.map((snapshot, index) => `${x(index)},${findingY(snapshot.totalFindings)}`).join(' ');

  return (
    <section className="analysis-trend" aria-label={t('analysisTrend')}>
      <div className="analysis-trend-header">
        <div>
          <h4>{t('analysisTrend')}</h4>
          <p>{t('analysisTrendDescription')}</p>
        </div>
        <div className="analysis-trend-legend" aria-hidden="true">
          <span className="score">{t('score')}</span>
          <span className="findings">{t('findingsNav')}</span>
        </div>
      </div>
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={t('analysisScoreTrend')}>
        <line x1={padding} y1={height - padding} x2={width - padding} y2={height - padding} className="trend-axis" />
        <polyline points={findingPoints} className="trend-line findings" />
        <polyline points={scorePoints} className="trend-line score" />
        {points.map((snapshot, index) => (
          <g key={snapshot.snapshotId}>
            <circle cx={x(index)} cy={scoreY(snapshot.score)} r="4" className="trend-point score">
              <title>{`${t('score')}: ${snapshot.score} · ${formatTimestamp(snapshot.generatedAtUtc)}`}</title>
            </circle>
            <circle cx={x(index)} cy={findingY(snapshot.totalFindings)} r="4" className="trend-point findings">
              <title>{`${t('findingsNav')}: ${snapshot.totalFindings} · ${formatTimestamp(snapshot.generatedAtUtc)}`}</title>
            </circle>
          </g>
        ))}
      </svg>
      <div className="analysis-trend-range">
        <span>{formatTimestamp(points[0].generatedAtUtc)}</span>
        <span>{formatTimestamp(points.at(-1)!.generatedAtUtc)}</span>
      </div>
    </section>
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

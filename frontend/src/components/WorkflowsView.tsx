import React from 'react';
import { GitBranch } from 'lucide-react';
import type {
  Activity,
  DependencySummary,
  Finding,
  FlowchartConversionApplyResult,
  FlowchartConversionResult,
  FlowchartConversionRollbackResult,
  getWorkflowHealth,
} from '../services/reportViewModel';
import {
  analyzeFlowchartConversion,
  applyFlowchartConversion,
  rollbackFlowchartConversion,
} from '../services/apiClient';
import { translate, type Locale } from '../localization';
import {
  Metric,
  complexityBadgeClass,
  estimateWorkflowScore,
  formatWorkflowStructure,
  localizeComplexityLevel,
} from './uiUtils';
import { FindingRow } from './FindingsView';
import {
  FlowchartConversionConfirmDialog,
  FlowchartConversionPanel,
  copyText,
  formatConversionPlan,
} from './FlowchartShared';

export function WorkflowHealth({
  workflows,
  t = (key, values) => translate('en', key, values),
}: {
  workflows: ReturnType<typeof getWorkflowHealth>;
  t?: (key: string, values?: Record<string, unknown>) => string;
}) {
  return (
    <table>
      <thead>
        <tr>
          <th>Workflow</th>
          <th>{t('findingsNav')}</th>
          <th>{t('activities')}</th>
          <th>{t('complexity')}</th>
        </tr>
      </thead>
      <tbody>
        {workflows.map((workflow) => (
          <tr key={workflow.relativePath}>
            <td>{workflow.relativePath}</td>
            <td>{workflow.findingCount}</td>
            <td>{workflow.activityCount}</td>
            <td>{localizeComplexityLevel(workflow.complexity?.complexityLevel, t)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export function WorkflowsView({
  projectPath,
  workflows,
  workflowCount,
  query,
  typeFilter,
  sort,
  categories,
  selectedWorkflow,
  allWorkflows,
  findings,
  dependencyAnalysis,
  locale,
  t,
  onQueryChange,
  onTypeFilterChange,
  onSortChange,
  onSelectWorkflow,
  onProjectChanged,
}: {
  projectPath: string;
  workflows: ReturnType<typeof getWorkflowHealth>;
  workflowCount: number;
  query: string;
  typeFilter: string;
  sort: string;
  categories: string[];
  selectedWorkflow: ReturnType<typeof getWorkflowHealth>[number] | null;
  allWorkflows: ReturnType<typeof getWorkflowHealth>;
  findings: Finding[];
  dependencyAnalysis: DependencySummary | null;
  locale: Locale;
  t: (key: string, values?: Record<string, unknown>) => string;
  onQueryChange: (value: string) => void;
  onTypeFilterChange: (value: string) => void;
  onSortChange: (value: string) => void;
  onSelectWorkflow: (workflow: ReturnType<typeof getWorkflowHealth>[number] | null) => void;
  onProjectChanged: () => void;
}) {
  const invocationGraph = React.useMemo(() => buildInvocationGraph(allWorkflows), [allWorkflows]);
  const selectedDetail = React.useMemo(
    () =>
      selectedWorkflow
        ? buildWorkflowDetail(selectedWorkflow, allWorkflows, findings, invocationGraph, dependencyAnalysis)
        : null,
    [allWorkflows, dependencyAnalysis, findings, invocationGraph, selectedWorkflow],
  );
  const [conversionResult, setConversionResult] = React.useState<FlowchartConversionResult | null>(null);
  const [conversionError, setConversionError] = React.useState<string | null>(null);
  const [conversionLoading, setConversionLoading] = React.useState(false);
  const [conversionApplyResult, setConversionApplyResult] = React.useState<FlowchartConversionApplyResult | null>(null);
  const [conversionRollbackResult, setConversionRollbackResult] = React.useState<FlowchartConversionRollbackResult | null>(null);
  const [conversionApplying, setConversionApplying] = React.useState(false);
  const [conversionRollingBack, setConversionRollingBack] = React.useState(false);
  const [confirmConversionOpen, setConfirmConversionOpen] = React.useState(false);

  const DEFAULT_DRAWER_WIDTH = 380;
  const MIN_DRAWER_WIDTH = 320;
  const MAX_DRAWER_WIDTH = 750;

  const [drawerWidth, setDrawerWidth] = React.useState<number>(() => {
    try {
      const saved = localStorage.getItem('rpa_workflow_drawer_width');
      const parsed = saved ? parseInt(saved, 10) : DEFAULT_DRAWER_WIDTH;
      return !isNaN(parsed) && parsed >= MIN_DRAWER_WIDTH && parsed <= MAX_DRAWER_WIDTH
        ? parsed
        : DEFAULT_DRAWER_WIDTH;
    } catch {
      return DEFAULT_DRAWER_WIDTH;
    }
  });

  const isDraggingRef = React.useRef(false);
  const startXRef = React.useRef(0);
  const startWidthRef = React.useRef(DEFAULT_DRAWER_WIDTH);

  const handleMouseDown = (e: React.MouseEvent) => {
    e.preventDefault();
    isDraggingRef.current = true;
    startXRef.current = e.clientX;
    startWidthRef.current = drawerWidth;
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';

    const onMouseMove = (moveEvent: MouseEvent) => {
      if (!isDraggingRef.current) return;
      const delta = startXRef.current - moveEvent.clientX;
      const nextWidth = Math.min(Math.max(startWidthRef.current + delta, MIN_DRAWER_WIDTH), MAX_DRAWER_WIDTH);
      setDrawerWidth(nextWidth);
    };

    const onMouseUp = () => {
      isDraggingRef.current = false;
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
      setDrawerWidth((curr) => {
        try {
          localStorage.setItem('rpa_workflow_drawer_width', String(curr));
        } catch {
          // ignore
        }
        return curr;
      });
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
  };

  const handleResetWidth = () => {
    setDrawerWidth(DEFAULT_DRAWER_WIDTH);
    try {
      localStorage.setItem('rpa_workflow_drawer_width', String(DEFAULT_DRAWER_WIDTH));
    } catch {
      // ignore
    }
  };

  React.useEffect(() => {
    if (!selectedWorkflow) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onSelectWorkflow(null);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [selectedWorkflow, onSelectWorkflow]);

  React.useEffect(() => {
    setConversionResult(null);
    setConversionError(null);
    setConversionApplyResult(null);
    setConversionRollbackResult(null);
    setConfirmConversionOpen(false);
  }, [selectedWorkflow?.relativePath]);

  async function handleAnalyzeConversion() {
    if (!selectedWorkflow || !projectPath) {
      return;
    }

    setConversionLoading(true);
    setConversionError(null);
    try {
      const result = (await analyzeFlowchartConversion({
        projectPath,
        workflowPath: selectedWorkflow.relativePath,
      })) as FlowchartConversionResult;
      setConversionResult(result);
      setConversionApplyResult(null);
      setConversionRollbackResult(null);
    } catch (error) {
      setConversionError(error instanceof Error ? error.message : t('flowchartConversionFailed'));
    } finally {
      setConversionLoading(false);
    }
  }

  async function handleApplyConversion() {
    if (!selectedWorkflow || !projectPath || !conversionResult) {
      return;
    }

    setConversionApplying(true);
    setConversionError(null);
    try {
      const result = (await applyFlowchartConversion({
        projectPath,
        workflowPath: selectedWorkflow.relativePath,
        expectedWorkflowHash: conversionResult.workflowHash,
        confirmed: true,
        createBackup: true,
      })) as FlowchartConversionApplyResult;
      setConversionApplyResult(result);
      setConfirmConversionOpen(false);
      onProjectChanged();
    } catch (error) {
      setConversionError(error instanceof Error ? error.message : t('flowchartConversionApplyFailed'));
    } finally {
      setConversionApplying(false);
    }
  }

  async function handleRollbackConversion() {
    if (!selectedWorkflow || !projectPath || !conversionApplyResult?.backupId) {
      return;
    }

    setConversionRollingBack(true);
    setConversionError(null);
    try {
      const result = (await rollbackFlowchartConversion({
        projectPath,
        workflowPath: selectedWorkflow.relativePath,
        backupId: conversionApplyResult.backupId,
        expectedCurrentHash: conversionApplyResult.convertedHash,
        createSafetyBackup: true,
      })) as FlowchartConversionRollbackResult;
      setConversionRollbackResult(result);
      onProjectChanged();
    } catch (error) {
      setConversionError(error instanceof Error ? error.message : t('flowchartConversionRollbackFailed'));
    } finally {
      setConversionRollingBack(false);
    }
  }

  return (
    <div className={`workflows-layout ${selectedWorkflow ? 'with-drawer' : 'single-column'}`}>
      <section className="list-panel">
        <div className="section-header">
          <div>
            <h2>{t('workflowCountFound', { count: workflowCount })}</h2>
            <p>{t('workflowListHelp')}</p>
          </div>
        </div>
        <div className="workflow-tools">
          <div className="search-input-wrapper">
            <input
              aria-label="Workflow search"
              value={query}
              onChange={(event) => onQueryChange(event.target.value)}
              placeholder={t('searchWorkflows')}
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
          <select
            aria-label="Workflow type filter"
            value={typeFilter}
            onChange={(event) => onTypeFilterChange(event.target.value)}
          >
            {categories.map((category) => (
              <option key={category} value={category}>
                {category === 'All' ? t('all') : category}
              </option>
            ))}
          </select>
          <select
            aria-label="Workflow sort"
            value={sort}
            onChange={(event) => onSortChange(event.target.value)}
          >
            <option value="findings">{t('byFindings')}</option>
            <option value="activities">{t('byActivity')}</option>
            <option value="name">{t('byName')}</option>
          </select>
          {(query || typeFilter !== 'All' || sort !== 'findings') && (
            <button
              type="button"
              className="reset-filters-btn"
              onClick={() => {
                onQueryChange('');
                onTypeFilterChange('All');
                onSortChange('findings');
              }}
            >
              {t('clearFilters')}
            </button>
          )}
        </div>
        <table className="clickable-table">
          <thead>
            <tr>
              <th>{t('workflowName')}</th>
              <th>{t('type')}</th>
              <th>{t('activity')}</th>
              <th>{t('findingsNav')}</th>
              <th>{t('complexity')}</th>
              <th>{t('score')}</th>
              <th>{t('status')}</th>
            </tr>
          </thead>
          <tbody>
            {workflows.map((workflow) => {
              const workflowScore = estimateWorkflowScore(workflow);
              return (
                <tr key={workflow.relativePath} onClick={() => onSelectWorkflow(workflow)}>
                  <td>{workflow.relativePath}</td>
                  <td>{formatWorkflowStructure(workflow.workflow, t)}</td>
                  <td>{workflow.activityCount}</td>
                  <td>{workflow.findingCount}</td>
                  <td>
                    <span
                      className={`status-badge ${complexityBadgeClass(workflow.complexity?.complexityLevel)}`}
                    >
                      {localizeComplexityLevel(workflow.complexity?.complexityLevel, t)}
                    </span>
                  </td>
                  <td>{workflowScore}</td>
                  <td>
                    <span
                      className={`status-badge ${workflowScore >= 85 ? 'good' : workflowScore >= 70 ? 'review' : 'risk'}`}
                    >
                      {workflowScore >= 85
                        ? t('good')
                        : workflowScore >= 70
                          ? t('reviewNeeded')
                          : t('risky')}
                    </span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </section>
      {selectedWorkflow && selectedDetail && (
        <>
          <div
            className="split-resize-handle"
            role="separator"
            aria-orientation="vertical"
            aria-label={t('resizePanel')}
            title={`${t('resizePanel')} (${t('resetPanelWidth')}: double click)`}
            onMouseDown={handleMouseDown}
            onDoubleClick={handleResetWidth}
          />
          <aside
            className="detail-drawer"
            aria-label="Workflow detail"
            style={{ width: `${drawerWidth}px`, flexShrink: 0 }}
          >
          <div className="section-header">
            <div>
              <span className="eyebrow">{t('workflowDetail')}</span>
              <h2>{getFileName(selectedWorkflow.relativePath)}</h2>
            </div>
            <button type="button" onClick={() => onSelectWorkflow(null)}>
              {t('backToWorkflows')}
            </button>
          </div>
          <div className="metric-grid compact">
            <Metric label={t('fileName')} value={getFileName(selectedWorkflow.relativePath)} />
            <Metric label={t('relativePath')} value={selectedWorkflow.relativePath} />
            <Metric label={t('activityCount')} value={selectedWorkflow.activityCount} />
            <Metric
              label={t('executableActivityCount')}
              value={selectedDetail.executableActivityCount}
            />
            <Metric label={t('findingCount')} value={selectedWorkflow.findingCount} />
            <Metric
              label={t('structureType')}
              value={selectedWorkflow.workflow.structureType ?? t('unknown')}
            />
            {selectedWorkflow.workflow.containsFlowchart && (
              <Metric
                label={t('flowchartCount')}
                value={selectedWorkflow.workflow.flowchartCount ?? 1}
              />
            )}
          </div>
          {selectedWorkflow.workflow.structureType === 'Flowchart' ? (
            <details open>
              <summary>{t('flowchartConversion')}</summary>
              <div className="conversion-actions">
                <button
                  type="button"
                  onClick={() => void handleAnalyzeConversion()}
                  disabled={conversionLoading || !projectPath}
                >
                  <GitBranch size={16} />
                  {conversionLoading ? t('analyzing') : t('previewConversion')}
                </button>
                {conversionResult?.plan && (
                  <button
                    type="button"
                    onClick={() => copyText(formatConversionPlan(conversionResult, t))}
                  >
                    {t('copyPlan')}
                  </button>
                )}
              </div>
              <p className="empty-state">{t('conversionPreviewOnly')}</p>
              {conversionError && <p className="error-text">{conversionError}</p>}
              {conversionResult && (
                <FlowchartConversionPanel
                  result={conversionResult}
                  applyResult={conversionApplyResult}
                  rollbackResult={conversionRollbackResult}
                  isApplying={conversionApplying}
                  isRollingBack={conversionRollingBack}
                  t={t}
                  onApply={() => setConfirmConversionOpen(true)}
                  onRollback={() => void handleRollbackConversion()}
                />
              )}
            </details>
          ) : selectedWorkflow.workflow.containsFlowchart ? (
            <details open>
              <summary>{t('flowchartConversion')}</summary>
              <p className="empty-state">{t('nestedFlowchartConversionNotSupported')}</p>
            </details>
          ) : null}
          {selectedDetail.complexity && (
            <details open>
              <summary>{t('complexity')}</summary>
              <div className="metric-grid compact">
                <Metric
                  label={t('complexityLevel')}
                  value={localizeComplexityLevel(selectedDetail.complexity.complexityLevel, t)}
                />
                <Metric
                  label={t('complexityScore')}
                  value={selectedDetail.complexity.complexityScore ?? 0}
                />
                <Metric
                  label={t('executableActivityCount')}
                  value={
                    selectedDetail.complexity.executableActivities ??
                    selectedDetail.executableActivityCount
                  }
                />
                <Metric
                  label={t('containerActivityCount')}
                  value={selectedDetail.complexity.containerActivities ?? 0}
                />
                <Metric
                  label={t('maxNestingDepth')}
                  value={selectedDetail.complexity.maxNestingDepth ?? 0}
                />
                <Metric
                  label={t('decisionCount')}
                  value={selectedDetail.complexity.decisionCount ?? 0}
                />
                <Metric label={t('loopCount')} value={selectedDetail.complexity.loopCount ?? 0} />
                <Metric
                  label={t('tryCatchCount')}
                  value={selectedDetail.complexity.tryCatchCount ?? 0}
                />
                <Metric
                  label={t('invokeWorkflowCount')}
                  value={selectedDetail.complexity.invokeWorkflowCount ?? 0}
                />
                <Metric
                  label={t('arguments')}
                  value={selectedDetail.complexity.argumentCount ?? selectedDetail.arguments.length}
                />
              </div>
            </details>
          )}
          <details open>
            <summary>{t('arguments')}</summary>
            {selectedDetail.arguments.length === 0 ? (
              <p className="empty-state">{t('noArguments')}</p>
            ) : (
              <table className="compact-table">
                <thead>
                  <tr>
                    <th>{t('name')}</th>
                    <th>{t('direction')}</th>
                    <th>{t('type')}</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedDetail.arguments.map((argument) => (
                    <tr key={`${argument.name}-${argument.direction ?? ''}`}>
                      <td>{argument.name}</td>
                      <td>{argument.direction ?? '-'}</td>
                      <td>{argument.type ?? '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </details>
          <details open>
            <summary>{t('invokedWorkflows')}</summary>
            {selectedDetail.invoked.length === 0 ? (
              <p className="empty-state">{t('noInvokes')}</p>
            ) : (
              <div className="chip-list">
                {selectedDetail.invoked.map((invoke) =>
                  invoke.targetWorkflow ? (
                    <button
                      key={`${invoke.sourceActivityId}-${invoke.rawPath}`}
                      type="button"
                      onClick={() => onSelectWorkflow(invoke.targetWorkflow!)}
                    >
                      {invoke.normalizedPath}
                    </button>
                  ) : (
                    <span
                      className={`workflow-chip ${invoke.isDynamic ? 'dynamic' : 'missing'}`}
                      key={`${invoke.sourceActivityId}-${invoke.rawPath}`}
                    >
                      {invoke.isDynamic ? t('dynamicWorkflowReference') : invoke.normalizedPath}
                    </span>
                  ),
                )}
              </div>
            )}
          </details>
          <details>
            <summary>{t('dependenciesUsed')}</summary>
            {selectedDetail.dependenciesUsed.length === 0 ? (
              <p className="empty-state">{t('noDependenciesUsed')}</p>
            ) : (
              <div className="chip-list">
                {selectedDetail.dependenciesUsed.map((dependency) => (
                  <span className="workflow-chip" key={dependency}>
                    {dependency}
                  </span>
                ))}
              </div>
            )}
          </details>
          <details>
            <summary>{t('calledBy')}</summary>
            {selectedDetail.callers.length === 0 ? (
              <p className="empty-state">{t('noCallers')}</p>
            ) : (
              <div className="chip-list">
                {selectedDetail.callers.map((caller) => (
                  <button
                    key={caller.relativePath}
                    type="button"
                    onClick={() => onSelectWorkflow(caller)}
                  >
                    {caller.relativePath}
                  </button>
                ))}
              </div>
            )}
          </details>
          <details>
            <summary>{t('activityTypes')}</summary>
            <div className="activity-type-grid">
              {selectedDetail.activityTypes.map((item) => (
                <Metric key={item.name} label={item.name} value={item.count} />
              ))}
            </div>
          </details>
          <details open>
            <summary>{t('activityTree')}</summary>
            {selectedDetail.activities.length === 0 ? (
              <p className="empty-state">{t('noActivities')}</p>
            ) : (
              <ActivityTree activities={selectedDetail.activities} />
            )}
          </details>
          <details open>
            <summary>{t('workflowFindings')}</summary>
            {selectedDetail.findings.length === 0 ? (
              <p className="empty-state">{t('noWorkflowFindings')}</p>
            ) : (
              selectedDetail.findings.map((finding) => (
                <FindingRow
                  key={`${finding.ruleId}-${finding.workflowPath}-${finding.activityId}-${finding.message}`}
                  finding={finding}
                  locale={locale}
                  t={t}
                />
              ))
            )}
          </details>
        </aside>
        </>
      )}
      {confirmConversionOpen && selectedWorkflow && conversionResult && (
        <FlowchartConversionConfirmDialog
          workflowPath={selectedWorkflow.relativePath}
          isApplying={conversionApplying}
          t={t}
          onCancel={() => setConfirmConversionOpen(false)}
          onConfirm={() => void handleApplyConversion()}
        />
      )}
    </div>
  );
}

interface WorkflowInvocation {
  rawPath: string;
  normalizedPath: string;
  sourceWorkflowPath: string;
  sourceActivityId?: string | null;
  isDynamic: boolean;
  targetWorkflow?: ReturnType<typeof getWorkflowHealth>[number];
}

function buildInvocationGraph(
  workflows: ReturnType<typeof getWorkflowHealth>,
): Map<string, WorkflowInvocation[]> {
  const workflowByPath = new Map(
    workflows.map((workflow) => [normalizeWorkflowPath(workflow.relativePath), workflow]),
  );
  const graph = new Map<string, WorkflowInvocation[]>();

  for (const workflow of workflows) {
    const invokes = (workflow.workflow.activities ?? [])
      .filter((activity) => normalizeActivityName(activity.name) === 'invokeworkflowfile')
      .map((activity) => {
        const rawPath =
          getActivityValue(activity, ['WorkflowFileName', 'WorkflowFile', 'FileName', 'Path']) ??
          '';
        const normalizedPath = normalizeWorkflowPath(stripQuotes(rawPath));
        const isDynamic = !normalizedPath || looksDynamic(rawPath);
        return {
          rawPath,
          normalizedPath,
          sourceWorkflowPath: workflow.relativePath,
          sourceActivityId: activity.activityId,
          isDynamic,
          targetWorkflow: !isDynamic ? workflowByPath.get(normalizedPath) : undefined,
        };
      });
    graph.set(normalizeWorkflowPath(workflow.relativePath), invokes);
  }

  return graph;
}

function buildWorkflowDetail(
  selected: ReturnType<typeof getWorkflowHealth>[number],
  allWorkflows: ReturnType<typeof getWorkflowHealth>,
  findings: Finding[],
  invocationGraph: Map<string, WorkflowInvocation[]>,
  dependencyAnalysis: DependencySummary | null,
) {
  const selectedPath = normalizeWorkflowPath(selected.relativePath);
  const activities = selected.workflow.activities ?? [];
  const workflowFindings = findings.filter(
    (finding) => normalizeWorkflowPath(finding.workflowPath ?? '') === selectedPath,
  );
  const invoked = invocationGraph.get(selectedPath) ?? [];
  const callers = allWorkflows.filter((workflow) =>
    (invocationGraph.get(normalizeWorkflowPath(workflow.relativePath)) ?? []).some(
      (invoke) =>
        invoke.targetWorkflow &&
        normalizeWorkflowPath(invoke.targetWorkflow.relativePath) === selectedPath,
    ),
  );
  const executableActivities = activities.filter(isExecutableActivity);
  const dependenciesUsed = (dependencyAnalysis?.packages ?? [])
    .filter((dependency) =>
      (dependency.usedByWorkflows ?? []).some(
        (workflow) => normalizeWorkflowPath(workflow) === selectedPath,
      ),
    )
    .map((dependency) => dependency.name)
    .sort((left, right) => left.localeCompare(right));

  return {
    activities: activities.filter(isActivityTreeVisible),
    arguments: selected.workflow.arguments ?? [],
    executableActivityCount: executableActivities.length,
    complexity: selected.complexity ?? selected.workflow.complexity ?? null,
    findings: workflowFindings,
    invoked,
    callers,
    dependenciesUsed,
    activityTypes: Array.from(
      executableActivities.reduce((map, activity) => {
        const name = activity.name || 'Activity';
        map.set(name, (map.get(name) ?? 0) + 1);
        return map;
      }, new Map<string, number>()),
    )
      .map(([name, count]) => ({ name, count }))
      .sort((left, right) => right.count - left.count || left.name.localeCompare(right.name))
      .slice(0, 12),
  };
}

function isActivityTreeVisible(activity: Activity): boolean {
  const name = normalizeActivityName(activity.name);
  const typeName = normalizeActivityName(activity.typeName ?? '');
  const hiddenNames = new Set([
    'assemblyreference',
    'variable',
    'visualbasicvalue',
    'visualbasicreference',
    'collection',
    'list',
    'cursorposition',
  ]);

  return !hiddenNames.has(name) && !hiddenNames.has(typeName);
}

export function ActivityTree({ activities }: { activities: Activity[] }) {
  const { childrenByParent, roots } = React.useMemo(() => {
    const children = new Map<string, Activity[]>();
    const ids = new Set(activities.map((activity) => activity.activityId));
    for (const activity of activities) {
      const parentKey =
        activity.parentActivityId && ids.has(activity.parentActivityId)
          ? activity.parentActivityId
          : '__root__';
      const siblings = children.get(parentKey);
      if (siblings) {
        siblings.push(activity);
      } else {
        children.set(parentKey, [activity]);
      }
    }

    return {
      childrenByParent: children,
      roots: children.get('__root__') ?? activities.filter((activity) => (activity.depth ?? 0) === 0),
    };
  }, [activities]);

  return (
    <ul className="activity-tree">
      {roots.map((activity) => (
        <ActivityTreeNode
          key={activity.activityId}
          activity={activity}
          childrenByParent={childrenByParent}
        />
      ))}
    </ul>
  );
}

export function ActivityTreeNode({
  activity,
  childrenByParent,
}: {
  activity: Activity;
  childrenByParent: Map<string, Activity[]>;
}) {
  const children = childrenByParent.get(activity.activityId) ?? [];
  const initiallyOpen = (activity.depth ?? 0) < 1;
  const [expanded, setExpanded] = React.useState(initiallyOpen);
  const label =
    activity.displayName && activity.displayName !== activity.name
      ? `${activity.name} · ${activity.displayName}`
      : activity.name;

  if (children.length === 0) {
    return (
      <li>
        <div>
          <span>{label}</span>
          <ActivityProperties activity={activity} />
        </div>
      </li>
    );
  }

  return (
    <li>
      <details open={expanded} onToggle={(event) => setExpanded(event.currentTarget.open)}>
        <summary>{label}</summary>
        <ActivityProperties activity={activity} />
        {expanded && (
          <ul>
            {children.map((child) => (
              <ActivityTreeNode
                key={child.activityId}
                activity={child}
                childrenByParent={childrenByParent}
              />
            ))}
          </ul>
        )}
      </details>
    </li>
  );
}

export function ActivityProperties({ activity }: { activity: Activity }) {
  const properties = Object.entries(activity.properties ?? {}).filter(
    ([, value]) => value !== null && value !== undefined && value !== '',
  );
  if (properties.length === 0) {
    return null;
  }

  return (
    <details className="activity-properties">
      <summary>Properties</summary>
      <dl>
        {properties.slice(0, 12).map(([name, value]) => (
          <React.Fragment key={name}>
            <dt>{name}</dt>
            <dd>{value}</dd>
          </React.Fragment>
        ))}
      </dl>
    </details>
  );
}

function getActivityValue(activity: Activity, names: string[]): string | undefined {
  const dictionaries = [activity.properties ?? {}, activity.arguments ?? {}];
  for (const dictionary of dictionaries) {
    for (const [key, value] of Object.entries(dictionary)) {
      if (
        names.some((name) => key.localeCompare(name, undefined, { sensitivity: 'accent' }) === 0) &&
        value
      ) {
        return value;
      }
    }
  }

  return undefined;
}

function normalizeActivityName(value: string): string {
  return value.replace(/\s+/g, '').toLowerCase();
}

function normalizeWorkflowPath(value: string): string {
  return stripQuotes(value)
    .replaceAll('\\', '/')
    .replace(/^\.\//, '')
    .normalize('NFC')
    .toLowerCase();
}

function stripQuotes(value: string): string {
  return value.trim().replace(/^["']|["']$/g, '');
}

function looksDynamic(value: string): boolean {
  const trimmed = value.trim();
  if (!trimmed) {
    return true;
  }

  return (
    /config\s*\(|path\.combine|string\.format|\+|\{|\}|\(|\)|\bin_|\bout_|\bvar_/i.test(trimmed) &&
    !/^["'][^"']+\.xaml["']$/i.test(trimmed)
  );
}

function isExecutableActivity(activity: Activity): boolean {
  const name = normalizeActivityName(activity.name);
  const typeName = normalizeActivityName(activity.typeName ?? '');
  const containerNames = new Set([
    'sequence',
    'flowchart',
    'trycatch',
    'catch',
    'finally',
    'if',
    'then',
    'else',
    'body',
    'while',
    'foreach',
    'dowhile',
    'pick',
    'parallel',
    'assemblyreference',
    'variable',
    'visualbasicvalue',
    'visualbasicreference',
    'collection',
    'list',
    'cursorposition',
  ]);

  return !containerNames.has(name) && !containerNames.has(typeName);
}

function getFileName(path: string): string {
  return path.replaceAll('\\', '/').split('/').pop() ?? path;
}

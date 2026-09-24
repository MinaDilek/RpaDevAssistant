import React from 'react';
import { FolderOpen, Play } from 'lucide-react';
import type { AnalysisResponse } from '../services/reportViewModel';
import { getComplexityDistribution, getTopComplexWorkflows } from '../services/reportViewModel';
import { Metric, buildReviewSummary, localizeComplexityLevel, type ActiveTab } from './uiUtils';

export function Overview({
  analysis,
  t,
  onNavigate,
  onOpenWorkflow,
}: {
  analysis: AnalysisResponse;
  t: (key: string, values?: Record<string, unknown>) => string;
  onNavigate?: (tab: ActiveTab) => void;
  onOpenWorkflow?: (workflowPath: string) => void;
}) {
  const criticalCount = analysis.analysis?.findings?.filter((finding) => finding.severity === 'Critical').length ?? 0;
  const highCount = analysis.analysis?.findings?.filter((finding) => finding.severity === 'Error').length ?? 0;
  const mediumCount = analysis.analysis?.findings?.filter((finding) => finding.severity === 'Warning').length ?? 0;
  const lowCount = analysis.analysis?.findings?.filter((finding) => finding.severity === 'Info' || finding.severity === 'Suggestion').length ?? 0;
  const passedChecks = Math.max(0, 25 - new Set((analysis.analysis?.findings ?? []).map((finding) => finding.ruleId)).size);
  const derivedComplexityDistribution = getComplexityDistribution(analysis);
  const complexityDistribution = analysis.complexitySummary ? {
    Low: analysis.complexitySummary.lowCount,
    Medium: analysis.complexitySummary.mediumCount,
    High: analysis.complexitySummary.highCount,
    VeryHigh: analysis.complexitySummary.veryHighCount,
  } : derivedComplexityDistribution;
  const topComplexWorkflows = analysis.complexitySummary?.topComplexWorkflows?.slice(0, 5).map((workflow) => ({
    relativePath: workflow.workflowPath,
    complexityScore: workflow.complexityScore,
    complexityLevel: workflow.complexityLevel,
    executableActivityCount: workflow.executableActivityCount,
    maxNestingDepth: workflow.maxNestingDepth,
  })) ?? getTopComplexWorkflows(analysis, 5).map((workflow) => ({
    relativePath: workflow.relativePath,
    complexityScore: workflow.complexity.complexityScore ?? 0,
    complexityLevel: workflow.complexity.complexityLevel ?? 'Low',
    executableActivityCount: workflow.complexity.executableActivities ?? workflow.workflow?.activityCount ?? 0,
    maxNestingDepth: workflow.complexity.maxNestingDepth ?? 0,
  }));

  return (
    <div className="overview-view">
      <div className="score-hero">
        <div>
          <span className="eyebrow">{t('projectOverview')}</span>
          <h2>{analysis.qualityScore?.score ?? 100} / 100</h2>
          <p>{t('grade')} {analysis.qualityScore?.grade ?? 'A'} · {analysis.compatibility ?? 'UiPath'} · {analysis.workflowCount ?? 0} {t('workflows')}</p>
        </div>
        <div className="severity-summary">
          <Metric label={t('critical')} value={criticalCount} />
          <Metric label={t('high')} value={highCount} />
          <Metric label={t('medium')} value={mediumCount} />
          <Metric label={t('low')} value={lowCount} />
          <Metric label={t('passedChecks')} value={passedChecks} />
        </div>
      </div>
      <div className="metric-grid">
        <Metric label={t('project')} value={analysis.projectName ?? 'Unknown'} />
        <button className="metric-button" type="button" onClick={() => onNavigate?.('workflows')}>
          <Metric label={t('workflows')} value={analysis.workflowCount ?? 0} />
        </button>
        <Metric label={t('activities')} value={analysis.totalActivityCount ?? 0} />
        <Metric label={t('score')} value={`${analysis.qualityScore?.score ?? 100} / 100`} />
        <Metric label={t('grade')} value={analysis.qualityScore?.grade ?? 'A'} />
        <button className="metric-button" type="button" onClick={() => onNavigate?.('findings')}>
          <Metric label={t('findingsNav')} value={analysis.analysis?.findings?.length ?? 0} />
        </button>
        <button className="metric-button" type="button" onClick={() => onNavigate?.('dependencies')}>
          <Metric label={t('dependencies')} value={analysis.dependencyAnalysis?.totalDependencies ?? 0} />
        </button>
      </div>
      {analysis.dependencyAnalysis && (
        <section className="insight-panel">
          <h2>{t('dependencyAnalysis')}</h2>
          <div className="metric-grid compact">
            <Metric label={t('usedDependencies')} value={analysis.dependencyAnalysis.usedDependencies} />
            <Metric label={t('possiblyUnused')} value={analysis.dependencyAnalysis.possiblyUnusedDependencies} />
            <Metric label={t('potentialConflicts')} value={analysis.dependencyAnalysis.potentialConflicts} />
            <Metric label={t('modernClassicMode')} value={analysis.dependencyAnalysis.modernClassicMode} />
          </div>
        </section>
      )}
      <section className="insight-panel">
        <h2>{t('reviewSummary')}</h2>
        <p>{buildReviewSummary(analysis, t)}</p>
      </section>
      <section className="insight-panel">
        <h2>{t('workflowComplexity')}</h2>
        <div className="metric-grid compact">
          {['Low', 'Medium', 'High', 'VeryHigh'].map((level) => (
            <Metric key={level} label={localizeComplexityLevel(level, t)} value={complexityDistribution[level] ?? 0} />
          ))}
        </div>
        {topComplexWorkflows.length > 0 && (
          <>
            <h3>{t('topComplexWorkflows')}</h3>
            <table>
              <thead>
                <tr>
                  <th>Workflow</th>
                  <th>{t('complexityScore')}</th>
                  <th>{t('complexityLevel')}</th>
                  <th>{t('executableActivityCount')}</th>
                  <th>{t('maxNestingDepth')}</th>
                </tr>
              </thead>
              <tbody>
                {topComplexWorkflows.map((item) => (
                  <tr
                    key={item.relativePath}
                    className={onOpenWorkflow ? 'clickable-row' : undefined}
                  >
                    <td>
                      {onOpenWorkflow ? (
                        <button className="link-button" type="button" onClick={() => onOpenWorkflow(item.relativePath)}>
                          {item.relativePath}
                        </button>
                      ) : item.relativePath}
                    </td>
                    <td>{item.complexityScore ?? 0}</td>
                    <td>{localizeComplexityLevel(item.complexityLevel, t)}</td>
                    <td>{item.executableActivityCount ?? 0}</td>
                    <td>{item.maxNestingDepth ?? 0}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
      </section>
    </div>
  );
}

export function DashboardEmpty({
  t,
  onAnalyze,
  onOpenProject,
}: {
  t: (key: string, values?: Record<string, unknown>) => string;
  onAnalyze: () => void;
  onOpenProject: () => void;
}) {
  return (
    <section className="empty-dashboard" aria-label="Getting started">
      <div className="welcome-card dashboard-hero">
        <span className="eyebrow">Local desktop review</span>
        <h2>{t('greeting')}</h2>
        <p>{t('homeSubtitle')}</p>
        <div className="dashboard-actions">
          <button className="primary-action" type="button" aria-label={t('dashboardAnalyzeProject')} onClick={onAnalyze}>
            <Play size={18} />
            {t('analyzeProject')}
          </button>
          <button type="button" onClick={onOpenProject}>
            <FolderOpen size={18} />
            {t('openProject')}
          </button>
        </div>
      </div>
      <div className="summary-strip">
        <Metric label={t('reviewedProjects')} value="12" />
        <Metric label={t('workflows')} value="247" />
        <Metric label={t('openFindings')} value="34" />
        <Metric label={t('averageQualityScore')} value="82 / 100" />
      </div>
      <section className="list-panel">
        <h2>{t('recentProjects')}</h2>
        <table>
          <thead>
            <tr>
              <th>{t('projectColumn')}</th>
              <th>{t('platformColumn')}</th>
              <th>{t('lastReviewColumn')}</th>
              <th>{t('qualityScoreColumn')}</th>
              <th>{t('findingsColumn')}</th>
              <th>{t('statusColumn')}</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>InvoiceAutomation</td>
              <td>UiPath</td>
              <td>{t('todayTime')}</td>
              <td>87</td>
              <td>12</td>
              <td><span className="status-badge good">{t('good')}</span></td>
            </tr>
            <tr>
              <td>CustomerOnboarding</td>
              <td>UiPath</td>
              <td>{t('yesterday')}</td>
              <td>74</td>
              <td>26</td>
              <td><span className="status-badge review">{t('reviewNeeded')}</span></td>
            </tr>
          </tbody>
        </table>
      </section>
    </section>
  );
}

import React from 'react';
import type { AnalysisResponse, getTopIssues, getWorkflowHealth } from '../services/reportViewModel';
import type { ReportFormat } from '../services/reportExportService';
import type { Locale } from '../localization';
import { Overview } from './Overview';
import { FindingRow } from './FindingsView';
import { WorkflowHealth } from './WorkflowsView';
import { Metric } from './uiUtils';

export function ReportView({
  analysis,
  topIssues,
  workflows,
  locale,
  t,
}: {
  analysis: AnalysisResponse;
  topIssues: ReturnType<typeof getTopIssues>;
  workflows: ReturnType<typeof getWorkflowHealth>;
  locale: Locale;
  t: (key: string, values?: Record<string, unknown>) => string;
}) {
  return (
    <div className="report-view">
      <Overview analysis={analysis} t={t} />
      <section>
        <h2>{t('summary')}</h2>
        <p>
          {analysis.analysis?.errorCount ?? 0} {t('errors')} ·{' '}
          {analysis.analysis?.warningCount ?? 0} {t('warnings')} ·{' '}
          {analysis.analysis?.suggestionCount ?? 0} {t('suggestions')}
        </p>
        <p>
          {t('profile')}: {analysis.qualityScore?.profileName ?? 'Default'}
        </p>
      </section>
      <section>
        <h2>{t('topIssues')}</h2>
        {topIssues.length === 0 ? (
          <p>{t('noIssuesDetected')}</p>
        ) : (
          topIssues.map((finding) => (
            <FindingRow
              key={`${finding.ruleId}-${finding.workflowPath}-${finding.activityDisplayName}-${finding.message}`}
              finding={finding}
              locale={locale}
              t={t}
            />
          ))
        )}
      </section>
      <section>
        <h2>{t('workflowHealth')}</h2>
        <WorkflowHealth workflows={workflows.slice(0, 8)} t={t} />
      </section>
      <section>
        <h2>{t('whyThisScore')}</h2>
        <div className="metric-grid compact">
          <Metric label={t('rawPenalty')} value={analysis.qualityScore?.rawPenalty ?? 0} />
          <Metric
            label={t('normalizedPenalty')}
            value={analysis.qualityScore?.normalizedPenalty ?? 0}
          />
          <Metric
            label={t('projectSizeFactor')}
            value={analysis.qualityScore?.projectSizeFactor ?? 1}
          />
        </div>
        {(analysis.qualityScore?.scoreBreakdown?.length ?? 0) > 0 && (
          <table>
            <thead>
              <tr>
                <th>{t('rule')}</th>
                <th>Severity</th>
                <th>{t('findingsNav')}</th>
                <th>{t('occurrenceCount')}</th>
                <th>Weight</th>
                <th>{t('rawPenalty')}</th>
                <th>{t('appliedPenalty')}</th>
                <th>{t('maxPenalty')}</th>
              </tr>
            </thead>
            <tbody>
              {analysis.qualityScore!.scoreBreakdown!.map((item) => (
                <tr key={item.ruleId}>
                  <td>
                    {item.ruleId} {item.ruleName}
                  </td>
                  <td>{item.severity}</td>
                  <td>{item.findingCount ?? 0}</td>
                  <td>{item.occurrenceCount ?? 0}</td>
                  <td>{item.weight ?? 0}</td>
                  <td>{item.rawPenalty ?? 0}</td>
                  <td>{item.appliedPenalty ?? 0}</td>
                  <td>{item.maxPenalty ?? 0}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}

export function ReportCreateDialog({
  t,
  onCancel,
  onExport,
}: {
  t: (key: string, values?: Record<string, unknown>) => string;
  onCancel: () => void;
  onExport: (format: ReportFormat) => void;
}) {
  const reportSections = [
    t('reportExecutiveSummary'),
    t('reportQualityScore'),
    t('findingsNav'),
    t('reportWorkflowDetails'),
    t('reportAiRecommendations'),
    t('reportFixSuggestions'),
  ];

  return (
    <div className="modal-backdrop" role="presentation">
      <section
        className="confirmation-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="report-create-title"
      >
        <h2 id="report-create-title">{t('reportDialogTitle')}</h2>
        <div className="settings-checks two-column">
          {reportSections.map((label) => (
            <label key={label}>
              <input type="checkbox" defaultChecked /> {label}
            </label>
          ))}
        </div>
        <p className="notice">{t('reportNotice')}</p>
        <div className="dialog-actions">
          <button type="button" onClick={onCancel}>
            {t('cancel')}
          </button>
          <button type="button" onClick={() => onExport('json')}>
            JSON
          </button>
          <button type="button" onClick={() => onExport('pdf')}>
            PDF
          </button>
          <button
            className="primary-action"
            type="button"
            onClick={() => onExport('html')}
          >
            {t('createHtmlReport')}
          </button>
        </div>
      </section>
    </div>
  );
}

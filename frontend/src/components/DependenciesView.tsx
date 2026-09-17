import React from 'react';
import { Package } from 'lucide-react';
import type { DependencyAnalysis, DependencySummary, Finding } from '../services/reportViewModel';
import type { Locale } from '../localization';
import { Metric } from './uiUtils';
import { FindingRow } from './FindingsView';

export function DependenciesView({
  dependencyAnalysis,
  findings,
  locale,
  t,
}: {
  dependencyAnalysis: DependencySummary | null;
  findings: Finding[];
  locale: Locale;
  t: (key: string, values?: Record<string, unknown>) => string;
}) {
  const [query, setQuery] = React.useState('');
  const [category, setCategory] = React.useState('All');
  const [usage, setUsage] = React.useState('All');
  const [risk, setRisk] = React.useState('All');
  const [selected, setSelected] = React.useState<DependencyAnalysis | null>(null);
  const packages = dependencyAnalysis?.packages ?? [];
  const categories = ['All', ...Array.from(new Set(packages.map((item) => item.category)))];
  const usages = ['All', ...Array.from(new Set(packages.map((item) => item.usageStatus)))];
  const risks = ['All', ...Array.from(new Set(packages.map((item) => item.riskLevel)))];
  const filtered = packages.filter((item) => {
    const text = `${item.name} ${item.declaredVersion ?? ''} ${item.category}`.toLowerCase();
    return (
      (!query.trim() || text.includes(query.trim().toLowerCase())) &&
      (category === 'All' || item.category === category) &&
      (usage === 'All' || item.usageStatus === usage) &&
      (risk === 'All' || item.riskLevel === risk)
    );
  });
  const relatedFindings = selected
    ? findings.filter(
        (finding) =>
          finding.currentValue?.includes(selected.name) ||
          finding.message?.includes(selected.name),
      )
    : [];

  if (!dependencyAnalysis) {
    return <p className="empty-state">{t('noDependencyAnalysis')}</p>;
  }

  return (
    <div className={`workflows-layout ${selected ? 'with-drawer' : 'single-column'}`}>
      <section className="list-panel">
        <div className="section-header">
          <div>
            <h2>{t('dependencyAnalysis')}</h2>
            <p>{t('dependencyAnalysisHelp')}</p>
          </div>
        </div>
        <div className="metric-grid compact">
          <Metric label={t('dependencies')} value={dependencyAnalysis.totalDependencies} />
          <Metric label={t('uiPathDependencies')} value={dependencyAnalysis.uiPathDependencies} />
          <Metric
            label={t('thirdPartyDependencies')}
            value={dependencyAnalysis.thirdPartyDependencies}
          />
          <Metric
            label={t('possiblyUnused')}
            value={dependencyAnalysis.possiblyUnusedDependencies}
          />
          <Metric
            label={t('potentialConflicts')}
            value={dependencyAnalysis.potentialConflicts}
          />
          <Metric
            label={t('modernClassicMode')}
            value={dependencyAnalysis.modernClassicMode}
          />
        </div>
        <div className="workflow-tools">
          <input
            aria-label={t('searchPackages')}
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder={t('searchPackages')}
          />
          <select
            aria-label={t('category')}
            value={category}
            onChange={(event) => setCategory(event.target.value)}
          >
            {categories.map((item) => (
              <option key={item} value={item}>
                {item === 'All' ? t('all') : item}
              </option>
            ))}
          </select>
          <select
            aria-label={t('usage')}
            value={usage}
            onChange={(event) => setUsage(event.target.value)}
          >
            {usages.map((item) => (
              <option key={item} value={item}>
                {item === 'All' ? t('all') : item}
              </option>
            ))}
          </select>
          <select
            aria-label={t('risk')}
            value={risk}
            onChange={(event) => setRisk(event.target.value)}
          >
            {risks.map((item) => (
              <option key={item} value={item}>
                {item === 'All' ? t('all') : item}
              </option>
            ))}
          </select>
        </div>
        <table className="clickable-table">
          <thead>
            <tr>
              <th>{t('package')}</th>
              <th>{t('version')}</th>
              <th>{t('category')}</th>
              <th>{t('usage')}</th>
              <th>{t('risk')}</th>
              <th>{t('usedByWorkflows')}</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((item) => (
              <tr key={item.name} onClick={() => setSelected(item)}>
                <td>
                  <Package size={14} /> {item.name}
                </td>
                <td>{item.declaredVersion ?? '-'}</td>
                <td>{item.category}</td>
                <td>{item.usageStatus}</td>
                <td>
                  <span
                    className={`status-badge ${item.riskLevel === 'Low' ? 'good' : item.riskLevel === 'Medium' ? 'review' : 'risk'}`}
                  >
                    {item.riskLevel}
                  </span>
                </td>
                <td>{item.usedByWorkflows?.length ?? 0}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
      {selected && (
        <aside className="detail-drawer" aria-label="Dependency detail">
          <div className="section-header">
            <div>
              <span className="eyebrow">{t('dependencyDetail')}</span>
              <h2>{selected.name}</h2>
            </div>
            <button type="button" onClick={() => setSelected(null)}>
              {t('close')}
            </button>
          </div>
          <div className="metric-grid compact">
            <Metric label={t('version')} value={selected.declaredVersion ?? '-'} />
            <Metric label={t('category')} value={selected.category} />
            <Metric label={t('usage')} value={selected.usageStatus} />
            <Metric label={t('risk')} value={selected.riskLevel} />
            <Metric label={t('compatibilityStatus')} value={selected.compatibilityStatus} />
            <Metric label={t('versionStatus')} value={selected.versionStatus} />
          </div>
          <details open>
            <summary>{t('usedActivities')}</summary>
            {(selected.usedActivities ?? []).length === 0 ? (
              <p className="empty-state">{t('noMappedActivities')}</p>
            ) : (
              <div className="chip-list">
                {selected.usedActivities!.map((activity) => (
                  <span className="workflow-chip" key={activity}>
                    {activity}
                  </span>
                ))}
              </div>
            )}
          </details>
          <details open>
            <summary>{t('usedByWorkflows')}</summary>
            {(selected.usedByWorkflows ?? []).length === 0 ? (
              <p className="empty-state">{t('noMappedWorkflows')}</p>
            ) : (
              <div className="chip-list">
                {selected.usedByWorkflows!.map((workflow) => (
                  <span className="workflow-chip" key={workflow}>
                    {workflow}
                  </span>
                ))}
              </div>
            )}
          </details>
          <details open>
            <summary>{t('compatibilityNotes')}</summary>
            {(selected.findings ?? []).length === 0 && !selected.notes ? (
              <p className="empty-state">{t('noDependencyRisks')}</p>
            ) : (
              <ul>
                {[...(selected.findings ?? []), selected.notes]
                  .filter(Boolean)
                  .map((note) => (
                    <li key={note}>{note}</li>
                  ))}
              </ul>
            )}
          </details>
          <details>
            <summary>{t('relatedFindings')}</summary>
            {relatedFindings.length === 0 ? (
              <p className="empty-state">{t('noRelatedFindings')}</p>
            ) : (
              relatedFindings.map((finding) => (
                <FindingRow
                  key={`${finding.ruleId}-${finding.currentValue}`}
                  finding={finding}
                  locale={locale}
                  t={t}
                />
              ))
            )}
          </details>
        </aside>
      )}
    </div>
  );
}

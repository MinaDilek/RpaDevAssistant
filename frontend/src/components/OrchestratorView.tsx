import React from 'react';
import { RefreshCw } from 'lucide-react';
import { getOrchestratorSummary } from '../services/apiClient';
import type { OrchestratorSummary } from '../services/reportViewModel';
import { Metric } from './uiUtils';

export function OrchestratorView({ t }: { t: (key: string, values?: Record<string, unknown>) => string }) {
  const [summary, setSummary] = React.useState<OrchestratorSummary | null>(null);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setSummary(await getOrchestratorSummary() as OrchestratorSummary);
    } catch (value) {
      setError(value instanceof Error ? value.message : t('orchestratorLoadFailed'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  React.useEffect(() => { void load(); }, [load]);

  return (
    <div className="list-panel orchestrator-view">
      <div className="section-header">
        <div><h2>{t('orchestrator')}</h2><p>{t('orchestratorHelp')}</p></div>
        <button type="button" onClick={() => void load()} disabled={loading}><RefreshCw size={16} />{loading ? t('loading') : t('refresh')}</button>
      </div>
      {error && <p className="error-text">{error}</p>}
      {summary && !summary.configured && <div className="notice"><strong>{t('notConfigured')}</strong><p>{t('orchestratorNotConfiguredHelp')}</p></div>}
      {summary?.success && <>
        <div className="metric-grid compact">
          <Metric label={t('deploymentType')} value={summary.deploymentType ?? '-'} />
          <Metric label={t('processes')} value={summary.processes.length} />
          <Metric label={t('queues')} value={summary.queues.length} />
          <Metric label={t('assets')} value={summary.assets.length} />
          <Metric label={t('machines')} value={summary.machines.length} />
        </div>
        <Inventory title={t('processes')} headers={[t('name'), t('processKey'), t('version')]} rows={summary.processes.map(item => [item.name, item.processKey ?? '-', item.version ?? '-'])} />
        <Inventory title={t('queues')} headers={[t('name'), t('maxRetries'), t('description')]} rows={summary.queues.map(item => [item.name, item.maxRetries ?? '-', item.description ?? '-'])} />
        <Inventory title={t('assets')} headers={[t('name'), t('scope'), t('type')]} rows={summary.assets.map(item => [item.name, item.valueScope ?? '-', item.valueType ?? '-'])} />
        <Inventory title={t('machines')} headers={[t('name'), t('type')]} rows={summary.machines.map(item => [item.name, item.type ?? '-'])} />
      </>}
    </div>
  );
}

function Inventory({ title, headers, rows }: { title: string; headers: string[]; rows: React.ReactNode[][] }) {
  return <section className="history-section"><h3>{title}</h3>{rows.length === 0 ? <p className="empty-state">-</p> : <table><thead><tr>{headers.map(header => <th key={header}>{header}</th>)}</tr></thead><tbody>{rows.map((row, rowIndex) => <tr key={rowIndex}>{row.map((cell, cellIndex) => <td key={cellIndex}>{cell}</td>)}</tr>)}</tbody></table>}</section>;
}

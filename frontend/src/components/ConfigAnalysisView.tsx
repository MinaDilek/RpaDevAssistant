import React from 'react';
import {
  AlertTriangle,
  Braces,
  CheckCircle2,
  ChevronDown,
  ClipboardCheck,
  Download,
  FileSpreadsheet,
  FolderOpen,
  Network,
  Play,
  RefreshCw,
  Trash2,
} from 'lucide-react';
import type {
  ConfigAnalysisResult,
  ConfigChangePreview,
  ConfigGenerateResult,
  ConfigUsage,
  HardCodedConfigCandidate,
  MissingConfigKey,
  UnusedConfigKey,
} from '../services/reportViewModel';
import {
  analyzeConfig,
  generateConfigWorkbook,
  previewConfigChanges,
} from '../services/apiClient';
import {
  selectConfigWorkbookFile,
  selectGeneratedConfigSavePath,
} from '../services/projectFolderService';
import { sanitizeFileName } from '../services/reportExportService';

export function ConfigAnalysisView({
  projectPath,
  desktop,
  t,
}: {
  projectPath: string;
  desktop: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
}) {
  const [result, setResult] = React.useState<ConfigAnalysisResult | null>(null);
  const [preview, setPreview] = React.useState<ConfigChangePreview | null>(null);
  const [generateResult, setGenerateResult] = React.useState<ConfigGenerateResult | null>(null);
  const [message, setMessage] = React.useState('');
  const [messageTone, setMessageTone] = React.useState<'info' | 'success' | 'error'>('info');
  const [configPath, setConfigPath] = React.useState('');
  const [loading, setLoading] = React.useState(false);
  const [selectedRemoveKeys, setSelectedRemoveKeys] = React.useState<Set<string>>(new Set());
  const [selectedAdditionIds, setSelectedAdditionIds] = React.useState<Set<string>>(new Set());
  const [outputPath, setOutputPath] = React.useState('');
  const [section, setSection] = React.useState<
    'overview' | 'graph' | 'unused' | 'missing' | 'hardcoded' | 'preview'
  >('overview');
  const selectedChangeCount = selectedRemoveKeys.size + selectedAdditionIds.size;

  async function runConfigAnalysis() {
    if (!projectPath) {
      setMessage(t('selectProjectFirst'));
      setMessageTone('error');
      return;
    }

    setLoading(true);
    setMessage('');
    setPreview(null);
    setGenerateResult(null);
    try {
      const next = (await analyzeConfig(
        projectPath,
        configPath.trim() || null,
      )) as ConfigAnalysisResult;
      setResult(next);
      if (next.configPath) {
        setConfigPath(next.configPath);
      }
      setSelectedRemoveKeys(new Set());
      setSelectedAdditionIds(new Set());
      setMessage(configAnalysisStatusMessage(next, configPath.trim(), t));
      setMessageTone(next.configFound ? 'success' : 'error');
    } catch (error) {
      console.error(error);
      setMessage(t('configAnalysisFailed'));
      setMessageTone('error');
    } finally {
      setLoading(false);
    }
  }

  async function runPreview() {
    if (!result) {
      setMessage(t('runConfigAnalysisFirst'));
      setMessageTone('error');
      return;
    }

    setLoading(true);
    setMessage('');
    try {
      const next = (await previewConfigChanges({
        projectPath,
        configPath: configPath.trim() || null,
        removeKeys: Array.from(selectedRemoveKeys),
        additions: buildSelectedAdditions(result, selectedAdditionIds),
      })) as ConfigChangePreview;
      setPreview(next);
      setSection('preview');
      setMessage(next.isValid ? t('configPreviewReady') : t('configPreviewInvalid'));
      setMessageTone(next.isValid ? 'success' : 'error');
    } catch (error) {
      console.error(error);
      setMessage(t('configPreviewFailed'));
      setMessageTone('error');
    } finally {
      setLoading(false);
    }
  }

  async function chooseConfigOutputPath() {
    const defaultName = result?.projectName
      ? `${sanitizeFileName(result.projectName)}-Config-Updated.xlsx`
      : 'Config-Updated.xlsx';
    const selected = desktop ? await selectGeneratedConfigSavePath(defaultName) : null;
    if (selected) {
      setOutputPath(selected);
    } else if (!desktop) {
      setMessage(t('configBrowserOutputHint'));
      setMessageTone('info');
    }
  }

  async function generateConfig() {
    if (!result) {
      setMessage(t('runConfigAnalysisFirst'));
      setMessageTone('error');
      return;
    }

    if (!outputPath.trim()) {
      setMessage(t('configOutputPathRequired'));
      setMessageTone('error');
      return;
    }

    setLoading(true);
    setMessage('');
    try {
      const next = (await generateConfigWorkbook({
        projectPath,
        configPath: configPath.trim() || null,
        outputPath: outputPath.trim(),
        removeKeys: Array.from(selectedRemoveKeys),
        additions: buildSelectedAdditions(result, selectedAdditionIds),
      })) as ConfigGenerateResult;
      setGenerateResult(next);
      setPreview(next.preview ?? preview);
      setMessage(next.success ? t('configGenerateCompleted') : t('configGenerateFailed'));
      setMessageTone(next.success ? 'success' : 'error');
    } catch (error) {
      console.error(error);
      setMessage(t('configGenerateFailed'));
      setMessageTone('error');
    } finally {
      setLoading(false);
    }
  }

  async function browseConfigWorkbook() {
    const selected = desktop ? await selectConfigWorkbookFile() : null;
    if (selected) {
      setConfigPath(selected);
      setMessage(t('configWorkbookSelected'));
      setMessageTone('info');
      return;
    }

    if (!desktop) {
      setMessage(t('configBrowserFileHint'));
      setMessageTone('info');
    }
  }

  function toggleUnusedKey(key: string) {
    setSelectedRemoveKeys(toggleSet(selectedRemoveKeys, key));
    setPreview(null);
    setGenerateResult(null);
  }

  function toggleHardcodedCandidate(id: string) {
    setSelectedAdditionIds(toggleSet(selectedAdditionIds, `hardcoded:${id}`));
    setPreview(null);
    setGenerateResult(null);
  }

  return (
    <div className="config-analysis-view">
      <section className="panel-card config-source-panel">
        <header className="config-page-heading">
          <div>
            <span className="eyebrow">{t('configAnalysis')}</span>
            <h2>{t('configIntelligenceTitle')}</h2>
            <p className="hint">{t('configIntelligenceHelp')}</p>
          </div>
          {result?.configFound && (
            <span className="config-ready-badge">
              <CheckCircle2 size={16} />
              {t('configReady')}
            </span>
          )}
        </header>

        <div className="config-source-workspace">
          <div className="field-group">
            <label htmlFor="configPath">{t('configWorkbook')}</label>
            <div className="path-row">
              <input
                id="configPath"
                value={configPath}
                onChange={(event) => setConfigPath(event.target.value)}
                placeholder={t('configWorkbookAutoDetect')}
              />
              <button
                type="button"
                onClick={() => void browseConfigWorkbook()}
                title={desktop ? t('browseConfigWorkbook') : t('browseTitleBrowser')}
              >
                <FolderOpen size={18} />
                {t('browse')}
              </button>
            </div>
            <p className="hint">{t('configWorkbookHint')}</p>
          </div>

          <div className="config-primary-actions">
            <button
              className="primary-action"
              type="button"
              onClick={() => void runConfigAnalysis()}
              disabled={loading || !projectPath}
            >
              <Play size={18} />
              {loading ? t('loading') : t('analyzeConfig')}
            </button>
            <button
              type="button"
              onClick={() => void runPreview()}
              disabled={loading || !result}
              aria-label={t('previewConfigChanges')}
            >
              <ClipboardCheck size={18} />
              {t('reviewSelectedChanges', { count: selectedChangeCount })}
            </button>
          </div>
        </div>

        {message && (
          <div className={`config-status-message ${messageTone}`} role="status">
            {messageTone === 'success' ? (
              <CheckCircle2 size={18} />
            ) : messageTone === 'error' ? (
              <AlertTriangle size={18} />
            ) : (
              <FileSpreadsheet size={18} />
            )}
            <span>{message}</span>
          </div>
        )}
      </section>

      {result ? (
        <>
          <nav className="config-section-nav" aria-label={t('configAnalysis')}>
            <button
              type="button"
              className={section === 'overview' ? 'active' : ''}
              onClick={() => setSection('overview')}
              aria-current={section === 'overview' ? 'page' : undefined}
              aria-label={t('overview')}
            >
              <FileSpreadsheet size={19} />
              <span>{t('overview')}</span>
              <strong>{result.overview.configKeyCount}</strong>
            </button>
            <button
              type="button"
              className={section === 'graph' ? 'active' : ''}
              onClick={() => setSection('graph')}
              aria-current={section === 'graph' ? 'page' : undefined}
              aria-label={t('configUsageGraph')}
            >
              <Network size={19} />
              <span>{t('configUsageGraph')}</span>
              <strong>{result.overview.usedKeyCount}</strong>
            </button>
            <button
              type="button"
              className={section === 'unused' ? 'active' : ''}
              onClick={() => setSection('unused')}
              aria-current={section === 'unused' ? 'page' : undefined}
              aria-label={t('unusedConfig')}
            >
              <Trash2 size={19} />
              <span>{t('unusedConfig')}</span>
              <strong>{result.overview.unusedKeyCount}</strong>
            </button>
            <button
              type="button"
              className={section === 'missing' ? 'active' : ''}
              onClick={() => setSection('missing')}
              aria-current={section === 'missing' ? 'page' : undefined}
              aria-label={t('missingConfig')}
            >
              <AlertTriangle size={19} />
              <span>{t('missingConfig')}</span>
              <strong>{result.overview.missingKeyCount}</strong>
            </button>
            <button
              type="button"
              className={section === 'hardcoded' ? 'active' : ''}
              onClick={() => setSection('hardcoded')}
              aria-current={section === 'hardcoded' ? 'page' : undefined}
              aria-label={t('hardcodedCandidates')}
            >
              <Braces size={19} />
              <span>{t('hardcodedCandidates')}</span>
              <strong>{result.overview.hardCodedCandidateCount}</strong>
            </button>
            <button
              type="button"
              className={section === 'preview' ? 'active' : ''}
              onClick={() => setSection('preview')}
              aria-current={section === 'preview' ? 'page' : undefined}
              aria-label={t('changePreview')}
            >
              <ClipboardCheck size={19} />
              <span>{t('changePreview')}</span>
              <strong>{selectedChangeCount}</strong>
            </button>
          </nav>

          {section === 'overview' && (
            <section className="panel-card config-overview-panel">
              <header className="section-header">
                <div>
                  <h2>{t('configOverviewTitle')}</h2>
                  <p className="hint">{t('configOverviewHelp')}</p>
                </div>
              </header>
              <div className="config-file-summary">
                <FileSpreadsheet size={24} />
                <div>
                  <span>{t('analyzedConfig')}</span>
                  <strong>{fileNameFromPath(result.configPath ?? '') || t('none')}</strong>
                  <code>{result.configPath ?? t('none')}</code>
                </div>
              </div>
              <div className="config-stat-grid">
                <button type="button" onClick={() => setSection('graph')}>
                  <span>{t('usedConfigKeys')}</span>
                  <strong>{result.overview.usedKeyCount}</strong>
                </button>
                <button type="button" onClick={() => setSection('unused')}>
                  <span>{t('unusedConfig')}</span>
                  <strong>{result.overview.unusedKeyCount}</strong>
                </button>
                <button type="button" onClick={() => setSection('missing')}>
                  <span>{t('missingConfig')}</span>
                  <strong>{result.overview.missingKeyCount}</strong>
                </button>
                <button type="button" onClick={() => setSection('hardcoded')}>
                  <span>{t('hardcodedCandidates')}</span>
                  <strong>{result.overview.hardCodedCandidateCount}</strong>
                </button>
              </div>
              {(result.messages ?? []).map((item) => (
                <p className="notice" key={item}>
                  {item}
                </p>
              ))}
            </section>
          )}

          {section === 'graph' && <ConfigUsageGraph usages={result.usages} t={t} />}

          {section === 'unused' && (
            <ConfigUnusedTable
              items={result.unusedKeys}
              selected={selectedRemoveKeys}
              t={t}
              onToggle={toggleUnusedKey}
            />
          )}

          {section === 'missing' && <ConfigMissingTable items={result.missingKeys} t={t} />}

          {section === 'hardcoded' && (
            <ConfigHardcodedTable
              items={result.hardCodedCandidates}
              selected={selectedAdditionIds}
              t={t}
              onToggle={toggleHardcodedCandidate}
            />
          )}

          {section === 'preview' && (
            <section className="panel-card">
              <header className="section-header config-preview-header">
                <div>
                  <h2>{t('changePreview')}</h2>
                  <p className="hint">
                    {t('selectedChangesSummary', { count: selectedChangeCount })}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => void runPreview()}
                  disabled={loading || !result}
                >
                  <RefreshCw size={17} />
                  {t('refreshPreview')}
                </button>
              </header>
              <div className="path-row">
                <input
                  aria-label={t('configOutputPath')}
                  value={outputPath}
                  onChange={(event) => setOutputPath(event.target.value)}
                  placeholder={t('configOutputPath')}
                />
                <button type="button" onClick={() => void chooseConfigOutputPath()}>
                  <FolderOpen size={18} />
                  {t('chooseOutputPath')}
                </button>
              </div>
              <p className="hint">{t('configOriginalUnchanged')}</p>
              {preview ? (
                <>
                  {!preview.isValid &&
                    preview.validationMessages.map((item) => (
                      <p className="error-text" key={item}>
                        {item}
                      </p>
                    ))}
                  <table>
                    <thead>
                      <tr>
                        <th>{t('type')}</th>
                        <th>{t('name')}</th>
                        <th>{t('before')}</th>
                        <th>{t('after')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {preview.changes.map((change) => (
                        <tr key={`${change.changeType}-${change.key}`}>
                          <td>{change.changeType}</td>
                          <td>{change.key}</td>
                          <td>{change.beforeValue ?? '-'}</td>
                          <td>{change.afterValue ?? '-'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </>
              ) : (
                <p className="empty-state">{t('noConfigPreview')}</p>
              )}
              <button
                className="primary-action config-generate-action"
                type="button"
                onClick={() => void generateConfig()}
                disabled={
                  loading || !result.canGenerate || !preview?.isValid || !outputPath.trim()
                }
              >
                <Download size={18} />
                {loading ? t('saving') : t('generateConfigWorkbook')}
              </button>
              {generateResult?.outputPath && (
                <p className="success-text">
                  {t('configGeneratedAt', { path: generateResult.outputPath })}
                </p>
              )}
            </section>
          )}
        </>
      ) : (
        <section className="config-empty-state">
          <FileSpreadsheet size={32} />
          <div>
            <strong>{t('configAnalysisEmptyTitle')}</strong>
            <p>{t('configAnalysisEmpty')}</p>
          </div>
        </section>
      )}
    </div>
  );
}

export function ConfigUsageGraph({
  usages,
  t,
}: {
  usages: ConfigUsage[];
  t: (key: string, values?: Record<string, unknown>) => string;
}) {
  const nodes = React.useMemo(() => buildConfigUsageGraph(usages, t('activity')), [t, usages]);
  return (
    <section className="panel-card">
      <h2>{t('configUsageGraph')}</h2>
      <p className="hint">{t('configUsageGraphHelp')}</p>
      {nodes.length === 0 ? (
        <p className="empty-state">{t('noConfigUsages')}</p>
      ) : (
        <div className="config-usage-graph">
          {nodes.map((node) => (
            <article className="config-usage-node" key={node.key}>
              <div className="config-key-node">
                <strong>{node.key}</strong>
                <span>{t('usageCount', { count: node.usageCount })}</span>
              </div>
              <div className="config-usage-edges">
                {node.workflows.map((workflow) => (
                  <div
                    className="config-workflow-node"
                    key={`${node.key}-${workflow.workflowPath}`}
                  >
                    <div>
                      <strong>{workflow.workflowPath}</strong>
                      <span>{t('configActivityCount', { count: workflow.activityCount })}</span>
                    </div>
                    <ul>
                      {workflow.activities.map((activity) => (
                        <li key={`${node.key}-${workflow.workflowPath}-${activity}`}>
                          {activity}
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

export function ConfigUnusedTable({
  items,
  selected,
  t,
  onToggle,
}: {
  items: UnusedConfigKey[];
  selected: Set<string>;
  t: (key: string, values?: Record<string, unknown>) => string;
  onToggle: (key: string) => void;
}) {
  const [query, setQuery] = React.useState('');
  const filteredItems = React.useMemo(() => {
    const normalizedQuery = query.trim().toLocaleLowerCase();
    return items
      .filter(({ entry }) =>
        !normalizedQuery ||
        [entry.key, entry.value, entry.description, entry.sheetName].some((value) =>
          value?.toLocaleLowerCase().includes(normalizedQuery),
        ),
      )
      .sort((left, right) => left.entry.key.localeCompare(right.entry.key));
  }, [items, query]);

  return (
    <section className="panel-card">
      <h2>{t('unusedConfig')}</h2>
      <p className="hint">{t('unusedConfigHelp')}</p>
      {items.length === 0 ? (
        <p className="empty-state">{t('noUnusedConfig')}</p>
      ) : (
        <>
          <div className="config-list-toolbar">
            <input
              aria-label={t('searchUnusedConfig')}
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={t('searchUnusedConfig')}
            />
            <span>
              {t('showingUnusedConfig', { shown: filteredItems.length, total: items.length })}
            </span>
          </div>
          {filteredItems.length === 0 ? (
            <p className="empty-state">{t('noUnusedConfig')}</p>
          ) : (
            <div className="config-candidate-list">
              {filteredItems.map(({ entry }) => (
                <article
                  className="config-candidate"
                  key={`${entry.sheetName}-${entry.rowNumber}-${entry.key}`}
                >
                  <details>
                    <summary>
                      <span className="config-candidate-summary">
                        <strong>{entry.key}</strong>
                        <span>
                          {t('configSheetAndRow', {
                            sheet: entry.sheetName,
                            row: entry.rowNumber,
                          })}
                        </span>
                      </span>
                      <ChevronDown size={18} aria-hidden="true" />
                    </summary>
                    <div className="config-candidate-details single-column">
                      <div>
                        <span>{t('value')}</span>
                        <code>{entry.value ?? '-'}</code>
                      </div>
                      {entry.description && (
                        <div>
                          <span>{t('description')}</span>
                          <p>{entry.description}</p>
                        </div>
                      )}
                      <p className="hint">{t('noParsedConfigReference')}</p>
                    </div>
                  </details>
                  <div className="config-candidate-action">
                    <label>
                      <input
                        type="checkbox"
                        checked={selected.has(entry.key)}
                        onChange={() => onToggle(entry.key)}
                        aria-label={`${t('remove')} ${entry.key}`}
                      />
                      <span>{t('remove')}</span>
                    </label>
                  </div>
                </article>
              ))}
            </div>
          )}
        </>
      )}
    </section>
  );
}

export function ConfigMissingTable({
  items,
  t,
}: {
  items: MissingConfigKey[];
  t: (key: string, values?: Record<string, unknown>) => string;
}) {
  return (
    <section className="panel-card">
      <h2>{t('missingConfig')}</h2>
      <p className="hint">{t('missingConfigHelp')}</p>
      {items.length === 0 ? (
        <p className="empty-state">{t('noMissingConfig')}</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>{t('status')}</th>
              <th>{t('name')}</th>
              <th>{t('usage')}</th>
              <th>Workflow</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.key}>
                <td>{t('manualChangeRequired')}</td>
                <td>{item.key}</td>
                <td>{item.references.length}</td>
                <td>{item.references[0]?.workflowPath ?? '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}

export function ConfigHardcodedTable({
  items,
  selected,
  t,
  onToggle,
}: {
  items: HardCodedConfigCandidate[];
  selected: Set<string>;
  t: (key: string, values?: Record<string, unknown>) => string;
  onToggle: (id: string) => void;
}) {
  return (
    <section className="panel-card">
      <h2>{t('hardcodedCandidates')}</h2>
      <p className="hint">{t('hardcodedCandidatesHelp')}</p>
      {items.length === 0 ? (
        <p className="empty-state">{t('noHardcodedCandidates')}</p>
      ) : (
        <div className="config-candidate-list">
          {items.map((item) => (
            <article className="config-candidate" key={item.id}>
              <details>
                <summary>
                  <span className="config-candidate-summary">
                    <strong>{item.type}</strong>
                    <span>{t('usageCount', { count: item.occurrenceCount })}</span>
                  </span>
                  <ChevronDown size={18} aria-hidden="true" />
                </summary>
                <div className="config-candidate-details">
                  <div>
                    <span>{t('value')}</span>
                    <code>{item.displayValue}</code>
                  </div>
                  <div>
                    <span>{t('ruleTemplate')}</span>
                    <strong>{item.suggestedKey || t('none')}</strong>
                  </div>
                  <div className="config-candidate-recommendation">
                    <span>{t('recommendation')}</span>
                    <p>{item.recommendation}</p>
                  </div>
                  {item.occurrences.length > 0 && (
                    <div className="config-candidate-occurrences">
                      <span>{t('usage')}</span>
                      <ul>
                        {item.occurrences.map((occurrence, index) => (
                          <li
                            key={`${occurrence.workflowPath}-${occurrence.activityId ?? occurrence.activityName ?? index}-${occurrence.propertyName ?? ''}`}
                          >
                            <strong>{occurrence.workflowPath}</strong>
                            <span>
                              {occurrence.activityDisplayName ??
                                occurrence.activityName ??
                                '-'} · {occurrence.propertyName ?? '-'}
                            </span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                </div>
              </details>
              <div className="config-candidate-action">
                {item.canAddToConfig ? (
                  <label>
                    <input
                      type="checkbox"
                      checked={selected.has(`hardcoded:${item.id}`)}
                      onChange={() => onToggle(item.id)}
                      aria-label={`${t('add')} ${item.suggestedKey}`}
                    />
                    <span>{t('add')}</span>
                  </label>
                ) : (
                  <span className="status-badge review">{t('manualChangeRequired')}</span>
                )}
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

function buildSelectedAdditions(
  result: ConfigAnalysisResult,
  selected: Set<string>,
): Array<{ key: string; value: string; description?: string; source?: string }> {
  const hardcoded = result.hardCodedCandidates
    .filter((item) => item.canAddToConfig && selected.has(`hardcoded:${item.id}`))
    .map((item) => ({
      key: item.suggestedKey,
      value: item.displayValue,
      description: item.recommendation,
      source: 'HardCodedCandidate',
    }));

  return hardcoded.filter((item) => item.key.trim());
}

function configAnalysisStatusMessage(
  result: ConfigAnalysisResult,
  requestedConfigPath: string,
  t: (key: string, values?: Record<string, unknown>) => string,
): string {
  if (result.configFound && result.configPath) {
    return t('configInUse', { fileName: fileNameFromPath(result.configPath) });
  }

  return requestedConfigPath ? t('selectedConfigInvalid') : t('configWorkbookNotFound');
}

function fileNameFromPath(path: string): string {
  return path.split(/[\\/]/).filter(Boolean).at(-1) ?? path;
}

function buildConfigUsageGraph(
  usages: ConfigUsage[],
  fallbackActivityLabel: string,
): Array<{
  key: string;
  usageCount: number;
  workflows: Array<{ workflowPath: string; activityCount: number; activities: string[] }>;
}> {
  const byKey = new Map<string, ConfigUsage[]>();
  usages.forEach((usage) => {
    byKey.set(usage.key, [...(byKey.get(usage.key) ?? []), usage]);
  });

  return Array.from(byKey.entries())
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([key, keyUsages]) => {
      const byWorkflow = new Map<string, ConfigUsage[]>();
      keyUsages.forEach((usage) => {
        byWorkflow.set(usage.workflowPath, [
          ...(byWorkflow.get(usage.workflowPath) ?? []),
          usage,
        ]);
      });

      return {
        key,
        usageCount: keyUsages.length,
        workflows: Array.from(byWorkflow.entries())
          .sort(([left], [right]) => left.localeCompare(right))
          .map(([workflowPath, workflowUsages]) => ({
            workflowPath,
            activityCount: workflowUsages.length,
            activities: workflowUsages
              .map(
                (usage) =>
                  usage.activityDisplayName ||
                  usage.activityName ||
                  usage.propertyName ||
                  fallbackActivityLabel,
              )
              .filter((value, index, values) => values.indexOf(value) === index)
              .slice(0, 8),
          })),
      };
    });
}

function toggleSet(current: Set<string>, value: string): Set<string> {
  const next = new Set(current);
  if (next.has(value)) {
    next.delete(value);
  } else {
    next.add(value);
  }

  return next;
}

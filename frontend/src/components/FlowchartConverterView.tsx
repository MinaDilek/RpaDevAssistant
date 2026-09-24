import React from 'react';
import {
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  Code2,
  Download,
  Eye,
  FileJson,
  FolderOpen,
  GitBranch,
  RefreshCw,
  Workflow,
} from 'lucide-react';
import type {
  StandaloneFlowchartAnalysisResult,
  StandaloneFlowchartConvertResult,
} from '../services/reportViewModel';
import {
  analyzeStandaloneFlowchart,
  convertStandaloneFlowchart,
} from '../services/apiClient';
import {
  selectConvertedWorkflowSavePath,
  selectXamlWorkflowFiles,
} from '../services/projectFolderService';
import { Metric } from './uiUtils';
import {
  PreviewTree,
  defaultConvertedPath,
  flowchartStandaloneStatusClass,
  localizeFlowchartConfidence,
  localizeFlowchartLevel,
  localizeFlowchartNarrative,
  localizeStandaloneFlowchartStatus,
  mergeStandaloneResults,
} from './FlowchartShared';
import {
  CustomActivityNoticeCard,
  FlowchartActivitiesBreakdown,
  FlowchartVisualSequenceFlow,
  FlowchartXamlCodePreview,
} from './FlowchartVisualPreview';

export function FlowchartConverterView({
  desktop,
  t,
}: {
  desktop: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
}) {
  const [xamlPath, setXamlPath] = React.useState('');
  const [results, setResults] = React.useState<StandaloneFlowchartAnalysisResult[]>([]);
  const [selectedPath, setSelectedPath] = React.useState<string | null>(null);
  const [isAnalyzing, setIsAnalyzing] = React.useState(false);
  const [isSaving, setIsSaving] = React.useState(false);
  const [message, setMessage] = React.useState<string | null>(null);
  const [messageKind, setMessageKind] = React.useState<'info' | 'success' | 'error'>('info');
  const [saveResult, setSaveResult] = React.useState<StandaloneFlowchartConvertResult | null>(null);
  const [outputPath, setOutputPath] = React.useState('');
  const [previewMode, setPreviewMode] = React.useState<'visual' | 'code'>('visual');
  const [replaceCustomActivities, setReplaceCustomActivities] = React.useState(true);
  const selected = results.find((item) => item.filePath === selectedPath) ?? results[0] ?? null;
  const selectedFlowchartNodes = selected?.graph?.nodes ?? [];
  const selectedHasFlowchart = selectedFlowchartNodes.length > 0;
  const customDetections = selected?.customActivityDetections ?? selected?.plan?.customActivityDetections ?? [];

  React.useEffect(() => {
    if (selected?.canConvert) {
      setOutputPath(defaultConvertedPath(selected.filePath, selected.suggestedOutputFileName));
    } else {
      setOutputPath('');
    }
  }, [selected?.filePath, selected?.suggestedOutputFileName, selected?.canConvert]);

  async function pickFiles() {
    const paths = await selectXamlWorkflowFiles();
    if (paths.length === 0) {
      setMessageKind('info');
      setMessage(desktop ? null : t('standaloneBrowserModeHint'));
      return;
    }

    setXamlPath(paths[0]);
    await analyzePaths(paths);
  }

  async function analyzeManualPath() {
    const path = xamlPath.trim();
    if (!path) {
      setMessageKind('error');
      setMessage(t('xamlPathRequired'));
      return;
    }

    await analyzePaths([path]);
  }

  async function analyzePaths(paths: string[]) {
    setIsAnalyzing(true);
    setMessage(null);
    setMessageKind('info');
    setSaveResult(null);
    try {
      const analyzed = (await Promise.all(
        paths.map(
          (path) =>
            analyzeStandaloneFlowchart({
              xamlFilePath: path,
            }) as Promise<StandaloneFlowchartAnalysisResult>,
        ),
      )) as StandaloneFlowchartAnalysisResult[];
      setResults((current) => mergeStandaloneResults(current, analyzed));
      setSelectedPath(analyzed[0]?.filePath ?? null);
      setMessageKind('success');
      setMessage(t('xamlAnalysisCompleted', { count: analyzed.length }));
    } catch (error) {
      setMessageKind('error');
      setMessage(
        error instanceof Error ? error.message : t('standaloneFlowchartAnalyzeFailed'),
      );
    } finally {
      setIsAnalyzing(false);
    }
  }

  async function chooseOutputPath() {
    if (!selected?.canConvert) {
      return null;
    }

    const suggested =
      outputPath.trim() ||
      defaultConvertedPath(selected.filePath, selected.suggestedOutputFileName);
    const chosen = desktop
      ? await selectConvertedWorkflowSavePath(suggested)
      : window.prompt(t('outputXamlPath'), suggested);
    if (chosen) {
      setOutputPath(chosen);
    }

    return chosen;
  }

  async function saveConvertedWorkflow() {
    if (!selected?.canConvert) {
      return;
    }

    let targetPath = outputPath.trim();
    if (!targetPath) {
      targetPath = (await chooseOutputPath()) ?? '';
      if (!targetPath) {
        setMessageKind('error');
        setMessage(t('outputXamlPathRequired'));
        return;
      }
    }

    setIsSaving(true);
    setMessage(null);
    setMessageKind('info');
    setSaveResult(null);
    try {
      const result = (await convertStandaloneFlowchart({
        xamlFilePath: selected.filePath,
        outputPath: targetPath,
        expectedWorkflowHash: selected.workflowHash,
        confirmed: true,
        replaceCustomActivitiesWithUiPathStandard: replaceCustomActivities,
      })) as StandaloneFlowchartConvertResult;
      setSaveResult(result);
      setMessageKind(result.success ? 'success' : 'error');
      setMessage(result.message);
      setResults((current) =>
        current.map((item) =>
          item.filePath === selected.filePath ? { ...item, status: 'Converted' } : item,
        ),
      );
    } catch (error) {
      setMessageKind('error');
      setMessage(
        error instanceof Error ? error.message : t('standaloneFlowchartConvertFailed'),
      );
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section
      className="results-panel flowchart-converter-view"
      aria-label={t('flowchartConverter')}
    >
      <div className="section-heading converter-heading">
        <div>
          <span className="eyebrow">{t('standaloneTool')}</span>
          <h2>{t('flowchartConverter')}</h2>
          <p>{t('flowchartConverterHelp')}</p>
        </div>
      </div>

      <ol className="converter-progress" aria-label={t('conversionProgress')}>
        <li className="active">
          <span>1</span>
          <div>
            <strong>{t('converterStepSelect')}</strong>
            <small>{t('converterStepSelectHint')}</small>
          </div>
        </li>
        <li className={selected ? 'active' : ''}>
          <span>2</span>
          <div>
            <strong>{t('converterStepAssess')}</strong>
            <small>{t('converterStepAssessHint')}</small>
          </div>
        </li>
        <li className={selected?.canConvert ? 'active' : ''}>
          <span>3</span>
          <div>
            <strong>{t('converterStepPreview')}</strong>
            <small>{t('converterStepPreviewHint')}</small>
          </div>
        </li>
      </ol>

      <section className="converter-stage" aria-labelledby="converter-step-one">
        <header className="converter-stage-header">
          <span>1</span>
          <div>
            <h3 id="converter-step-one">{t('converterStepSelect')}</h3>
            <p>{t('converterStepSelectDescription')}</p>
          </div>
        </header>
        <div className="converter-source-row">
          <label className="field-group" htmlFor="standaloneXamlPath">
            <span>{t('xamlFilePath')}</span>
            <input
              id="standaloneXamlPath"
              value={xamlPath}
              onChange={(event) => setXamlPath(event.target.value)}
              placeholder={t('selectXamlFirst')}
            />
          </label>
          <button
            type="button"
            className="icon-text-button"
            onClick={() => void pickFiles()}
            title={desktop ? t('selectXamlFile') : t('browseTitleBrowser')}
          >
            <FolderOpen size={18} />
            {t('selectXamlFile')}
          </button>
          <button
            className="primary-action icon-text-button"
            type="button"
            onClick={() => void analyzeManualPath()}
            disabled={isAnalyzing || !xamlPath.trim()}
          >
            {isAnalyzing ? (
              <RefreshCw className="spin" size={18} />
            ) : (
              <GitBranch size={18} />
            )}
            {isAnalyzing ? t('analyzing') : t('analyzeXaml')}
          </button>
        </div>
        {!desktop && <p className="hint">{t('standaloneBrowserModeHint')}</p>}
        {message && (
          <div className={`converter-message ${messageKind}`} role="status">
            {messageKind === 'error' ? <AlertTriangle size={18} /> : <CheckCircle2 size={18} />}
            <span>{message}</span>
          </div>
        )}
        {results.length > 0 && (
          <div className="converter-file-section">
            <div className="converter-subheading">
              <h4>{t('selectedXamlFiles')}</h4>
              <span>{t('selectedFileCount', { count: results.length })}</span>
            </div>
            <div className="converter-file-list">
              {results.map((item) => (
                <button
                  type="button"
                  key={item.filePath}
                  className={selected?.filePath === item.filePath ? 'selected' : ''}
                  onClick={() => setSelectedPath(item.filePath)}
                >
                  <FileJson size={20} />
                  <span className="converter-file-name">
                    <strong>{item.fileName}</strong>
                    <small title={item.filePath}>{item.filePath}</small>
                  </span>
                  <span>{item.structureType}</span>
                  <span>
                    {item.activityCount ?? 0} {t('activities')}
                  </span>
                  <span
                    className={`status-badge ${flowchartStandaloneStatusClass(item.status)}`}
                  >
                    {localizeStandaloneFlowchartStatus(item.status, t)}
                  </span>
                </button>
              ))}
            </div>
          </div>
        )}
      </section>

      <section
        className={`converter-stage ${selected ? '' : 'inactive'}`}
        aria-labelledby="converter-step-two"
      >
        <header className="converter-stage-header">
          <span>2</span>
          <div>
            <h3 id="converter-step-two">{t('converterStepAssess')}</h3>
            <p>{t('converterStepAssessDescription')}</p>
          </div>
        </header>
        {!selected ? (
          <div className="converter-empty">
            <GitBranch size={28} />
            <p>{t('flowchartConverterEmpty')}</p>
          </div>
        ) : (
          <>
            <div className="converter-selected-summary">
              <div>
                <strong>{selected.fileName}</strong>
                <span title={selected.filePath}>{selected.filePath}</span>
              </div>
              <span
                className={`status-badge ${flowchartStandaloneStatusClass(selected.status)}`}
              >
                {localizeStandaloneFlowchartStatus(selected.status, t)}
              </span>
            </div>
            <div className="converter-metrics">
              <Metric label={t('currentStructure')} value={selected.structureType} />
              <Metric label={t('activityCount')} value={selected.activityCount ?? 0} />
              <Metric label={t('arguments')} value={selected.argumentCount ?? 0} />
              <Metric
                label={t('nodes')}
                value={selected.flowchartNodeCount ?? selected.graph?.nodes.length ?? 0}
              />
              <Metric
                label={t('decisions')}
                value={selected.decisionCount ?? selected.graph?.decisions.length ?? 0}
              />
              <Metric
                label={t('switches')}
                value={selected.switchCount ?? selected.graph?.switches.length ?? 0}
              />
            </div>
            {selected.assessment && (
              <div className="converter-assessment-grid">
                <div>
                  <span>{t('convertibility')}</span>
                  <strong>
                    {localizeFlowchartLevel(selected.assessment.conversionLevel, t)}
                  </strong>
                </div>
                <div>
                  <span>{t('confidence')}</span>
                  <strong>
                    {localizeFlowchartConfidence(selected.assessment.confidence, t)}
                  </strong>
                </div>
                <div>
                  <span>{t('cycles')}</span>
                  <strong>{selected.graph?.hasCycles ? t('yes') : t('no')}</strong>
                </div>
                <div>
                  <span>{t('unreachableNodes')}</span>
                  <strong>{selected.graph?.hasUnreachableNodes ? t('yes') : t('no')}</strong>
                </div>
              </div>
            )}
            {selected.assessment?.reasons?.length ? (
              <div className="converter-reasons">
                <h4>{t('assessmentReasons')}</h4>
                <ul>
                  {selected.assessment.reasons.map((reason) => (
                    <li key={reason}>{localizeFlowchartNarrative(reason, t)}</li>
                  ))}
                </ul>
              </div>
            ) : null}
            {!selected.canConvert && (
              <div className="converter-message warning">
                <AlertTriangle size={19} />
                <div>
                  <strong>{t('conversionNotAvailable')}</strong>
                  <p>
                    {t(
                      selectedHasFlowchart
                        ? 'conversionNotAvailableNestedReason'
                        : 'conversionNotAvailableReason',
                    )}
                  </p>
                  {(selected.messages?.length ?? 0) > 0 && (
                    <ul>
                      {selected.messages?.map((item) => (
                        <li key={item}>{localizeFlowchartNarrative(item, t)}</li>
                      ))}
                    </ul>
                  )}
                </div>
              </div>
            )}
            {selectedHasFlowchart && !selected.assessment && (
              <details className="converter-details">
                <summary>{t('detectedFlowchartNodes')}</summary>
                <ul className="compact-list">
                  {selectedFlowchartNodes.slice(0, 12).map((node) => (
                    <li key={node.id}>
                      {node.id} · {node.type} · {node.displayName ?? node.activityName ?? '-'}
                    </li>
                  ))}
                </ul>
              </details>
            )}
            {selected.assessment && (
              <details className="converter-details">
                <summary>{t('risks')}</summary>
                {selected.assessment.risks.length === 0 ? (
                  <p>{t('noRisks')}</p>
                ) : (
                  <ul>
                    {selected.assessment.risks.map((risk) => (
                      <li key={risk}>{localizeFlowchartNarrative(risk, t)}</li>
                    ))}
                  </ul>
                )}
              </details>
            )}
          </>
        )}
      </section>

      <section
        className={`converter-stage ${selected?.canConvert ? '' : 'inactive'}`}
        aria-labelledby="converter-step-three"
      >
        <header className="converter-stage-header">
          <span>3</span>
          <div>
            <h3 id="converter-step-three">{t('converterStepPreview')}</h3>
            <p>{t('converterStepPreviewDescription')}</p>
          </div>
        </header>
        {!selected ? (
          <div className="converter-empty">
            <p>{t('selectAndAnalyzeBeforePreview')}</p>
          </div>
        ) : !selected.canConvert ? (
          <div className="converter-empty unsupported">
            <AlertTriangle size={26} />
            <p>{t('manualReviewRequired')}</p>
          </div>
        ) : (
          <>
            {/* Conversion Target Summary Banner */}
            <div className="converter-summary-banner">
              <div className="summary-banner-item">
                <span className="summary-banner-label">{t('currentStructure')}</span>
                <span className="summary-banner-val">{selected.structureType}</span>
              </div>
              <ArrowRight size={18} className="summary-banner-arrow" />
              <div className="summary-banner-item">
                <span className="summary-banner-label">{t('workflowTargetSequence')}</span>
                <span className="summary-banner-val highlight">Sequence</span>
              </div>
              <div className="summary-banner-divider" />
              <div className="summary-banner-item">
                <span className="summary-banner-label">{t('convertibility')}</span>
                <span className="summary-banner-val">{localizeFlowchartLevel(selected.assessment?.conversionLevel, t)}</span>
              </div>
            </div>

            {/* Custom Dependency Activities Detection and Standard UiPath Replacement Banner */}
            {customDetections.length > 0 && (
              <CustomActivityNoticeCard
                detections={customDetections}
                replaceCustomActivities={replaceCustomActivities}
                onToggleReplace={setReplaceCustomActivities}
                t={t}
              />
            )}

            {/* Activities Used in Conversion (Kullanılacak Aktiviteler) */}
            {selected.plan?.previewTree && (
              <FlowchartActivitiesBreakdown
                tree={selected.plan.previewTree}
                customDetections={customDetections}
                replaceCustomActivities={replaceCustomActivities}
                t={t}
              />
            )}

            {/* Visual Workflow & Code Preview Workspace */}
            <div className="converter-preview-workspace">
              <div className="preview-workspace-header">
                <div className="preview-header-title">
                  <Workflow size={18} className="text-primary" />
                  <h4>{t('proposedSequence')}</h4>
                  <span className="preview-mode-tag">{t('visualWorkflowPreview')}</span>
                </div>
                <div className="preview-view-tabs" role="tablist" aria-label={t('visualWorkflowPreview')}>
                  <button
                    type="button"
                    role="tab"
                    aria-selected={previewMode === 'visual'}
                    className={`preview-tab-btn ${previewMode === 'visual' ? 'active' : ''}`}
                    onClick={() => setPreviewMode('visual')}
                  >
                    <Eye size={15} />
                    <span>{t('visualFlow')}</span>
                  </button>
                  <button
                    type="button"
                    role="tab"
                    aria-selected={previewMode === 'code'}
                    className={`preview-tab-btn ${previewMode === 'code' ? 'active' : ''}`}
                    onClick={() => setPreviewMode('code')}
                  >
                    <Code2 size={15} />
                    <span>{t('xamlCodePreview')}</span>
                  </button>
                </div>
              </div>

              <div className="preview-workspace-content">
                {selected.plan?.previewTree ? (
                  previewMode === 'visual' ? (
                    <div className="visual-flow-wrapper">
                      <FlowchartVisualSequenceFlow
                        node={selected.plan.previewTree}
                        customDetections={customDetections}
                        replaceCustomActivities={replaceCustomActivities}
                      />
                    </div>
                  ) : (
                    <FlowchartXamlCodePreview
                      node={selected.plan.previewTree}
                      customDetections={customDetections}
                      replaceCustomActivities={replaceCustomActivities}
                      t={t}
                    />
                  )
                ) : (
                  <p className="empty-state">{t('noPreviewAvailable')}</p>
                )}
              </div>
            </div>
            <div className="converter-plan-details">
              <details open>
                <summary>{t('steps')}</summary>
                <ul>
                  {selected.plan?.steps.map((step) => (
                    <li key={step}>{localizeFlowchartNarrative(step, t)}</li>
                  ))}
                </ul>
              </details>
              <details>
                <summary>{t('conversionMappings')}</summary>
                <div className="table-scroll">
                  <table className="compact-table">
                    <thead>
                      <tr>
                        <th>{t('sourceNode')}</th>
                        <th>{t('targetPath')}</th>
                        <th>{t('type')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(selected.plan?.mappings ?? []).map((mapping) => (
                        <tr key={`${mapping.sourceNodeId}-${mapping.targetPath}`}>
                          <td>{mapping.sourceNodeId}</td>
                          <td>{mapping.targetPath}</td>
                          <td>{mapping.transformationType}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </details>
            </div>
            <div className="converter-save-row">
              <label className="field-group" htmlFor="standaloneOutputPath">
                <span>{t('outputXamlPath')}</span>
                <input
                  id="standaloneOutputPath"
                  value={outputPath}
                  onChange={(event) => setOutputPath(event.target.value)}
                  placeholder={t('chooseOutputPathFirst')}
                />
              </label>
              <button
                type="button"
                className="icon-text-button"
                onClick={() => void chooseOutputPath()}
              >
                <FolderOpen size={18} />
                {t('chooseOutputPath')}
              </button>
              <button
                className="primary-action icon-text-button"
                type="button"
                onClick={() => void saveConvertedWorkflow()}
                disabled={isSaving || !outputPath.trim()}
              >
                <Download size={18} />
                {isSaving ? t('saving') : t('convertAndSave')}
              </button>
            </div>
            <p className="converter-safety-note">
              <CheckCircle2 size={16} />
              {t('originalFileNotModified')}
            </p>
          </>
        )}
        {saveResult && (
          <div className={`converter-save-result ${saveResult.success ? 'success' : 'error'}`}>
            <strong>
              {saveResult.success
                ? t('convertedWorkflowSaved')
                : t('conversionWasNotApplied')}
            </strong>
            <p>{saveResult.message}</p>
            {saveResult.outputPath && (
              <p className="path-value">
                {t('outputXamlPath')}: {saveResult.outputPath}
              </p>
            )}
            <p>
              {t('currentStructure')}: {saveResult.originalStructure} → {saveResult.newStructure}
            </p>
          </div>
        )}
      </section>
    </section>
  );
}

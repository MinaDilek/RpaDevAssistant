import React from 'react';
import { FileSearch, FolderOpen, Play, ShieldCheck } from 'lucide-react';
import { analyzeProcessPdd } from '../services/apiClient';
import { selectPddDocumentFile } from '../services/projectFolderService';
import type { AnalysisResponse, ProcessPddAnalysisResult } from '../services/reportViewModel';
import type { Locale } from '../localization';
import { Metric } from './uiUtils';

type Translator = (key: string, values?: Record<string, unknown>) => string;

export function ProcessPddAnalysisView({
  analysis,
  projectPath,
  desktop,
  locale,
  t,
}: {
  analysis: AnalysisResponse | null;
  projectPath: string;
  desktop: boolean;
  locale: Locale;
  t: Translator;
}) {
  const [pddPath, setPddPath] = React.useState('');
  const [pddFileName, setPddFileName] = React.useState('');
  const [pddContent, setPddContent] = React.useState('');
  const [result, setResult] = React.useState<ProcessPddAnalysisResult | null>(null);
  const [statusFilter, setStatusFilter] = React.useState('All');
  const [ruleStatusFilter, setRuleStatusFilter] = React.useState('All');
  const [confidenceFilter, setConfidenceFilter] = React.useState('All');
  const [workflowFilter, setWorkflowFilter] = React.useState('All');
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState('');

  React.useEffect(() => {
    setResult(null);
  }, [projectPath, pddPath, pddContent]);

  async function selectDesktopPdd() {
    const selected = await selectPddDocumentFile();
    if (!selected) return;
    setPddPath(selected);
    setPddFileName(selected.split(/[\\/]/).pop() ?? selected);
    setPddContent('');
    setError('');
  }

  async function selectBrowserPdd(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;
    setPddPath('');
    setPddFileName(file.name);
    setPddContent(await file.text());
    setError('');
  }

  async function runReview() {
    if (!analysis || (!pddPath && !pddContent)) return;
    setLoading(true);
    setError('');
    try {
      const response = await analyzeProcessPdd({ projectPath, pddPath: pddPath || undefined, pddFileName, pddContent: pddContent || undefined });
      setResult(response as ProcessPddAnalysisResult);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t('processPddFailed'));
    } finally {
      setLoading(false);
    }
  }

  if (!analysis) {
    return <p className="empty-state">{t('processPddAnalyzeFirst')}</p>;
  }

  const workflows = result ? ['All', ...Array.from(new Set(result.projectBusinessRules.map((rule) => rule.workflowPath))).sort()] : ['All'];
  const statusByRuleId = new Map(result?.gapAnalysis.map((item) => [item.projectRule.id, item.status]) ?? []);
  const filteredRules = result?.projectBusinessRules.filter((rule) =>
    (confidenceFilter === 'All' || rule.confidence === confidenceFilter) &&
    (workflowFilter === 'All' || rule.workflowPath === workflowFilter) &&
    (ruleStatusFilter === 'All' || statusByRuleId.get(rule.id) === ruleStatusFilter)) ?? [];
  const filteredGaps = result?.gapAnalysis.filter((item) =>
    statusFilter === 'All' || item.status === statusFilter) ?? [];
  const missingCount = result?.gapAnalysis.filter((item) => item.status === 'PossiblyMissing').length ?? 0;

  return (
    <div className="process-pdd-page">
      <section className="process-pdd-intake">
        <div className="section-header">
          <div>
            <span className="eyebrow">{t('processPddEyebrow')}</span>
            <h2>{t('processPddTitle')}</h2>
            <p>{t('processPddHelp')}</p>
          </div>
          <FileSearch size={28} />
        </div>
        <div className="process-pdd-context-grid">
          <div className="field-group">
            <label>{t('project')}</label>
            <div className="readonly-field">{analysis.projectName ?? projectPath}</div>
          </div>
          <div className="field-group">
            <label>{t('pddDocument')}</label>
            {desktop ? (
              <div className="path-row">
                <input value={pddPath} readOnly placeholder={t('selectPdd')} />
                <button type="button" onClick={() => void selectDesktopPdd()}><FolderOpen size={17} />{t('browse')}</button>
              </div>
            ) : (
              <input aria-label={t('pddDocument')} type="file" accept=".txt,.md,text/plain,text/markdown" onChange={(event) => void selectBrowserPdd(event)} />
            )}
            <p className="hint">{t('processPddSupportedFormats')}</p>
          </div>
        </div>
        <button className="primary-action" type="button" disabled={loading || (!pddPath && !pddContent)} onClick={() => void runReview()}>
          <Play size={17} />{loading ? t('processPddAnalyzing') : t('processPddAnalyze')}
        </button>
        {error && <p className="error-message" role="alert">{error}</p>}
      </section>

      {result && (
        <>
          <section className="process-pdd-overview">
            <div className="section-header"><div><h3>{t('overview')}</h3><p>{t('processPddReadOnly')}</p></div><ShieldCheck size={22} /></div>
            <div className="metric-grid compact">
              <Metric label={t('workflows')} value={result.workflowCount} />
              <Metric label={t('identifiedSystems')} value={result.systems.length} />
              <Metric label={t('projectBusinessRules')} value={result.projectBusinessRules.length} />
              <Metric label={t('pddBusinessRules')} value={result.pddBusinessRules.length} />
              <Metric label={t('possiblyMissingRules')} value={missingCount} />
            </div>
            <div className="association-line"><strong>{t('project')}:</strong> {result.projectName}<span /><strong>PDD:</strong> {result.pddFileName}</div>
          </section>

          <section className="process-pdd-section">
            <h3>{t('processSummary')}</h3>
            <p>{result.processSummary}</p>
          </section>

          <section className="process-pdd-section">
            <h3>{t('systemsUsed')}</h3>
            {result.systems.length === 0 ? <p className="empty-state">{t('noEvidenceFound')}</p> : (
              <div className="compact-table"><div className="table-head"><span>{t('processSystem')}</span><span>{t('processSystemType')}</span><span>{t('processEvidence')}</span><span>{t('processUsedByWorkflows')}</span></div>
                {result.systems.map((system) => <div className="table-row" key={`${system.type}:${system.name}`}><strong>{system.name}</strong><span>{system.type}</span><span>{system.evidence}</span><span>{system.workflowPaths.join(', ')}</span></div>)}
              </div>
            )}
          </section>

          <section className="process-pdd-section">
            <h3>{t('processFlow')}</h3>
            <p className="hint">{t('processFlowDisclaimer')}</p>
            {result.processFlow.length === 0
              ? <p className="empty-state">{t('noEvidenceFound')}</p>
              : <ol className="process-flow-list">{result.processFlow.map((step) => <li key={`${step.order}:${step.workflowPath}`}><strong>{step.title}</strong><span>{step.workflowPath}</span><small>{step.evidence}</small></li>)}</ol>}
            {result.omittedProcessFlowCount > 0 && <p className="hint">{t('processFlowOmitted', { count: result.omittedProcessFlowCount })}</p>}
          </section>

          <section className="process-pdd-section">
            <div className="section-header"><div><h3>{t('projectBusinessRules')}</h3><p>{t('evidenceBackedRules')}</p></div></div>
            <div className="workflow-tools">
              <select aria-label={t('processConfidence')} value={confidenceFilter} onChange={(event) => setConfidenceFilter(event.target.value)}><option value="All">{t('allConfidence')}</option><option>High</option><option>Medium</option><option>Low</option></select>
              <select aria-label={t('workflow')} value={workflowFilter} onChange={(event) => setWorkflowFilter(event.target.value)}>{workflows.map((workflow) => <option key={workflow}>{workflow === 'All' ? t('processAllWorkflows') : workflow}</option>)}</select>
              <select aria-label={t('processRuleStatus')} value={ruleStatusFilter} onChange={(event) => setRuleStatusFilter(event.target.value)}><option value="All">{t('allStatuses')}</option><option value="PossiblyMissing">{t('possiblyMissingFromPdd')}</option><option value="PossiblyDocumented">{t('possiblyDocumented')}</option><option value="NeedsReview">{t('needsReview')}</option><option value="Documented">{t('documented')}</option></select>
            </div>
            {filteredRules.length === 0
              ? <p className="empty-state">{t('noMatchingBusinessRules')}</p>
              : <div className="evidence-list">{filteredRules.map((rule) => {
                const ruleStatus = statusByRuleId.get(rule.id);
                return <article key={rule.id}><div className="evidence-title"><strong>{rule.title}</strong><span className={`status-badge ${rule.confidence.toLowerCase()}`}>{rule.confidence}</span></div><p>{rule.description}</p><code>{rule.condition}</code><small>{rule.evidence}</small>{ruleStatus && <span className={`status-badge ${ruleStatus.toLowerCase()}`}>{t(`pddStatus${ruleStatus}`)}</span>}</article>;
              })}</div>}
          </section>

          <section className="process-pdd-section">
            <h3>{t('pddBusinessRules')}</h3>
            {result.pddBusinessRules.length === 0
              ? <p className="empty-state">{t('noPddBusinessRules')}</p>
              : <div className="evidence-list">{result.pddBusinessRules.map((rule) => <article key={rule.id}><strong>{rule.title}</strong><p>{rule.sourceSnippet}</p><small>{rule.sourceReference}</small></article>)}</div>}
          </section>

          <section className="process-pdd-section">
            <div className="section-header"><div><h3>{t('pddGapAnalysis')}</h3><p>{t('gapAnalysisDisclaimer')}</p></div></div>
            <div className="workflow-tools"><select aria-label={t('processStatus')} value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}><option value="All">{t('allStatuses')}</option><option value="PossiblyMissing">{t('possiblyMissingFromPdd')}</option><option value="PossiblyDocumented">{t('possiblyDocumented')}</option><option value="NeedsReview">{t('needsReview')}</option><option value="Documented">{t('documented')}</option></select></div>
            {filteredGaps.length === 0
              ? <p className="empty-state">{t('noMatchingBusinessRules')}</p>
              : <div className="evidence-list">{filteredGaps.map((item) => <article key={item.projectRule.id}><div className="evidence-title"><strong>{item.projectRule.title}</strong><span className={`status-badge ${item.status.toLowerCase()}`}>{t(`pddStatus${item.status}`)}</span></div><p>{item.reason}</p><small>{item.projectRule.evidence}</small>{item.matchedPddRule && <div className="pdd-match"><strong>{t('matchedPddEvidence')}</strong><p>{item.matchedPddRule.sourceSnippet}</p><small>{item.matchedPddRule.sourceReference}</small></div>}{item.suggestedPddAddition && <div className="pdd-suggestion"><strong>{t('pddAdditionSuggestion')}</strong><p>{item.suggestedPddAddition}</p></div>}</article>)}</div>}
          </section>
        </>
      )}
    </div>
  );
}

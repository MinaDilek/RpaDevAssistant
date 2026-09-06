import React from 'react';
import ReactDOM from 'react-dom/client';
import { Bell, Bot, ChevronDown, Download, FileJson, FolderOpen, GitBranch, History, Package, Play, RefreshCw, RotateCcw, Send, Settings, Wrench } from 'lucide-react';
import { analyzeFlowchartConversion, analyzeProject, analyzeStandaloneFlowchart, applyFix, applyFlowchartConversion, askProject, checkHealth, compareAnalysisSnapshots, convertStandaloneFlowchart, exportCustomRules, exportRuleProfiles, getBackendBaseUrl, getFixSuggestion, getRuleProfiles, getRules, importCustomRules, listAnalysisHistory, listBackups, rollbackFlowchartConversion, runAiReview, saveCustomRule, saveRuleProfile, setApiLocale, testCustomRule, undoFix, validateProject } from './services/apiClient';
import { isTauriDesktop, selectConvertedWorkflowSavePath, selectProjectFolder, selectXamlWorkflowFiles } from './services/projectFolderService';
import { exportReport, type ReportFormat } from './services/reportExportService';
import { getComplexityDistribution, getTopComplexWorkflows, getTopIssues, getWorkflowHealth, type Activity, type AiReviewResult, type AnalysisComparison, type AnalysisHistoryList, type AnalysisResponse, type AnalysisSnapshotSummary, type BackupListResult, type BackupSummary, type CustomRuleDefinition, type CustomRuleTestResult, type DependencyAnalysis, type DependencySummary, type Finding, type FixApplyResult, type FixSuggestion, type FixSuggestionResult, type FlowchartConversionApplyResult, type FlowchartConversionResult, type FlowchartConversionRollbackResult, type FlowchartPreviewNode, type ProjectAnswer, type RuleCatalogItem, type RuleProfile, type StandaloneFlowchartAnalysisResult, type StandaloneFlowchartConvertResult, type UndoResult } from './services/reportViewModel';
import { localizeFinding, translate, type Locale } from './localization';
import './styles.css';

type HealthState = 'checking' | 'ready' | 'unavailable';
type ActiveTab = 'overview' | 'findings' | 'history' | 'workflows' | 'dependencies' | 'flowchartConverter' | 'report' | 'ai' | 'ask' | 'rules' | 'settings';

export function App() {
  const [projectPath, setProjectPath] = React.useState('');
  const [selectedFolder, setSelectedFolder] = React.useState<string | null>(null);
  const [healthState, setHealthState] = React.useState<HealthState>('checking');
  const [statusMessage, setStatusMessage] = React.useState('Checking backend...');
  const [analysis, setAnalysis] = React.useState<AnalysisResponse | null>(null);
  const [isAnalyzing, setIsAnalyzing] = React.useState(false);
  const [exportingFormat, setExportingFormat] = React.useState<ReportFormat | null>(null);
  const [aiReview, setAiReview] = React.useState<AiReviewResult | null>(null);
  const [aiReviewLoading, setAiReviewLoading] = React.useState(false);
  const [projectQuestion, setProjectQuestion] = React.useState('');
  const [questionHistory, setQuestionHistory] = React.useState<Array<{ question: string; answer: ProjectAnswer }>>([]);
  const [askLoading, setAskLoading] = React.useState(false);
  const [fixResult, setFixResult] = React.useState<FixSuggestionResult | null>(null);
  const [applyResult, setApplyResult] = React.useState<FixApplyResult | null>(null);
  const [isApplyingFix, setIsApplyingFix] = React.useState(false);
  const [pendingApplyFix, setPendingApplyFix] = React.useState<FixSuggestion | null>(null);
  const [backups, setBackups] = React.useState<BackupSummary[]>([]);
  const [analysisSnapshots, setAnalysisSnapshots] = React.useState<AnalysisSnapshotSummary[]>([]);
  const [selectedComparison, setSelectedComparison] = React.useState<AnalysisComparison | null>(null);
  const [comparisonLoading, setComparisonLoading] = React.useState(false);
  const [historyLoading, setHistoryLoading] = React.useState(false);
  const [pendingUndo, setPendingUndo] = React.useState<BackupSummary | null>(null);
  const [undoResult, setUndoResult] = React.useState<UndoResult | null>(null);
  const [isUndoing, setIsUndoing] = React.useState(false);
  const [analysisOutOfDate, setAnalysisOutOfDate] = React.useState(false);
  const [selectedFixFinding, setSelectedFixFinding] = React.useState<Finding | null>(null);
  const [fixLoadingKey, setFixLoadingKey] = React.useState<string | null>(null);
  const [activeTab, setActiveTab] = React.useState<ActiveTab>('overview');
  const [selectedProfileId, setSelectedProfileId] = React.useState('default');
  const [language, setLanguage] = React.useState<Locale>(() => {
    const saved = safeGetStoredLocale();
    if (saved === 'tr' || saved === 'en') {
      return saved;
    }

    return navigator.language.toLowerCase().startsWith('tr') ? 'tr' : 'en';
  });
  const [avatarMenuOpen, setAvatarMenuOpen] = React.useState(false);
  const [reportModalOpen, setReportModalOpen] = React.useState(false);
  const [findingSeverityFilter, setFindingSeverityFilter] = React.useState('All');
  const [findingCategoryFilter, setFindingCategoryFilter] = React.useState('All');
  const [workflowQuery, setWorkflowQuery] = React.useState('');
  const [workflowTypeFilter, setWorkflowTypeFilter] = React.useState('All');
  const [workflowSort, setWorkflowSort] = React.useState('findings');
  const [selectedWorkflow, setSelectedWorkflow] = React.useState<ReturnType<typeof getWorkflowHealth>[number] | null>(null);
  const [settingsSection, setSettingsSection] = React.useState('profileSettings');
  const [rules, setRules] = React.useState<RuleCatalogItem[]>([]);
  const [rulesLoading, setRulesLoading] = React.useState(false);
  const [selectedRule, setSelectedRule] = React.useState<RuleCatalogItem | null>(null);
  const [ruleStatus, setRuleStatus] = React.useState('');
  const [profiles, setProfiles] = React.useState<RuleProfile[]>([]);

  const desktop = isTauriDesktop();
  const t = React.useCallback((key: string, values?: Record<string, unknown>) => translate(language, key, values), [language]);
  const findings = analysis?.analysis?.findings ?? [];
  const topIssues = getTopIssues(findings);
  const workflowHealth = analysis ? getWorkflowHealth(analysis) : [];
  const workflowCategories = React.useMemo(() => ['All', 'Main', 'Framework', 'Business', 'Utility', 'Unused'], []);
  const filteredWorkflows = React.useMemo(() => {
    const query = workflowQuery.trim().toLowerCase();
    return workflowHealth
      .filter((workflow) => !query || workflow.relativePath.toLowerCase().includes(query))
      .filter((workflow) => workflowTypeFilter === 'All' || getWorkflowType(workflow.relativePath) === workflowTypeFilter)
      .sort((left, right) => {
        if (workflowSort === 'name') {
          return left.relativePath.localeCompare(right.relativePath);
        }

        if (workflowSort === 'activities') {
          return right.activityCount - left.activityCount || left.relativePath.localeCompare(right.relativePath);
        }

        return right.findingCount - left.findingCount || left.relativePath.localeCompare(right.relativePath);
      });
  }, [workflowHealth, workflowQuery, workflowSort, workflowTypeFilter]);
  const findingCategories = React.useMemo(() => ['All', ...Array.from(new Set(findings.map((finding) => finding.category).filter(Boolean))) as string[]], [findings]);
  const filteredFindings = React.useMemo(() => findings.filter((finding) => {
    const severityMatches = findingSeverityFilter === 'All' || finding.severity === findingSeverityFilter;
    const categoryMatches = findingCategoryFilter === 'All' || finding.category === findingCategoryFilter;
    return severityMatches && categoryMatches;
  }), [findings, findingCategoryFilter, findingSeverityFilter]);

  const refreshHealth = React.useCallback(async () => {
    setHealthState('checking');
    const healthy = await checkHealth();
    setHealthState(healthy ? 'ready' : 'unavailable');
    setStatusMessage(healthy ? t('backendReady', { url: await getBackendBaseUrl() }) : t('backendUnavailable'));
  }, [t]);

  React.useEffect(() => {
    void refreshHealth();
  }, [refreshHealth]);

  React.useEffect(() => {
    safeStoreLocale(language);
    setApiLocale(language);
  }, [language]);

  React.useEffect(() => {
    void refreshRules();
    void refreshProfiles();
  }, [language]);

  async function refreshRules() {
    setRulesLoading(true);
    try {
      const result = (await getRules()) as RuleCatalogItem[];
      setRules(result);
      setSelectedRule((current) => current ? result.find((rule) => rule.id === current.id) ?? current : result[0] ?? null);
    } catch {
      setRuleStatus(t('ruleCatalogLoadFailed'));
    } finally {
      setRulesLoading(false);
    }
  }

  async function refreshProfiles() {
    try {
      const result = (await getRuleProfiles()) as RuleProfile[];
      setProfiles(result);
      if (result.length > 0 && !result.some((profile) => profile.id === selectedProfileId)) {
        setSelectedProfileId(result[0].id);
      }
    } catch {
      setRuleStatus(t('profileLoadFailed'));
    }
  }

  async function browseProjectFolder() {
    if (!desktop) {
      setStatusMessage(t('browserModeHint'));
      return;
    }

    const folder = await selectProjectFolder();
    if (!folder) {
      return;
    }

    setProjectPath(folder);
    setSelectedFolder(folder);
    if (healthState !== 'ready') {
      setStatusMessage(t('folderSelectedBackendNotReady'));
      return;
    }

    try {
      const validation = await validateProject(folder);
      setStatusMessage(validation.looksLikeUiPathProject ? t('folderSelectedProjectJson') : t('notUipathProject'));
    } catch {
      setStatusMessage(t('validationFailed'));
    }
  }

  async function runAnalysis() {
    if (!projectPath.trim()) {
      setStatusMessage(t('selectProjectFirst'));
      return;
    }

    if (healthState !== 'ready') {
      setStatusMessage(t('backendNotReady'));
      return;
    }

    setIsAnalyzing(true);
    setAnalysis(null);
    try {
      const result = (await analyzeProject(projectPath.trim(), selectedProfileId)) as AnalysisResponse;
      setAnalysis(result);
      setAnalysisOutOfDate(false);
      setAiReview(null);
      setQuestionHistory([]);
      setFixResult(null);
      setApplyResult(null);
      await refreshBackups(projectPath.trim());
      await refreshAnalysisHistory();
      setSelectedComparison(result.comparisonWithPrevious ?? null);
      setSelectedFixFinding(null);
      setActiveTab('report');
      setStatusMessage(result.projectName ? t('analysisCompleted') : t('notUipathProject'));
    } catch {
      setStatusMessage(t('analysisFailed'));
    } finally {
      setIsAnalyzing(false);
    }
  }

  async function runAiReviewFromUi(scope: 'Project' | 'Workflow', workflowPath?: string) {
    if (!projectPath.trim() || !analysis) {
      setStatusMessage(t('staticAnalysisBeforeAi'));
      return;
    }

    setAiReviewLoading(true);
    setActiveTab('ai');
    try {
      const result = await runAiReview({
        projectPath: projectPath.trim(),
        profileId: selectedProfileId,
        scope,
        workflowPath,
      }) as AiReviewResult;
      setAiReview(result);
      setStatusMessage(result.isSuccess === false ? (result.errorMessage ?? t('aiReviewFailed')) : t('aiReviewCompleted'));
    } catch (error) {
      setStatusMessage(error instanceof Error ? error.message : t('aiReviewFailed'));
    } finally {
      setAiReviewLoading(false);
    }
  }

  async function viewFixSuggestion(finding: Finding, useAi = false) {
    if (!projectPath.trim() || !analysis) {
      setStatusMessage('Run analysis before requesting fix suggestions.');
      return;
    }

    const loadingKey = `${finding.ruleId}-${finding.workflowPath ?? ''}-${finding.activityId ?? ''}-${useAi}`;
    setFixLoadingKey(loadingKey);
    setSelectedFixFinding(finding);
    setActiveTab('findings');
    try {
      const result = await getFixSuggestion({
        projectPath: projectPath.trim(),
        profileId: selectedProfileId,
        ruleId: finding.ruleId,
        workflowPath: finding.workflowPath,
        activityId: finding.activityId,
        propertyName: finding.propertyName,
        useAi,
      }) as FixSuggestionResult;
      setFixResult(result);
      setApplyResult(null);
      setStatusMessage(result.suggestion ? t('fixSuggestionGenerated') : (result.message ?? t('noFixAvailable')));
    } catch (error) {
      setStatusMessage(error instanceof Error ? error.message : t('fixSuggestionFailed'));
    } finally {
      setFixLoadingKey(null);
    }
  }

  async function applySelectedFix(suggestion: FixSuggestion) {
    if (!projectPath.trim()) {
      setStatusMessage('Select or enter a UiPath project folder first.');
      return;
    }

    if (!suggestion.workflowPath || !suggestion.propertyName || !suggestion.suggestedValue) {
      setStatusMessage('Fix suggestion is missing required apply details.');
      return;
    }

    setIsApplyingFix(true);
    try {
      const result = await applyFix({
        projectPath: projectPath.trim(),
        fixSuggestionId: suggestion.id,
        ruleId: suggestion.ruleId,
        workflowPath: suggestion.workflowPath,
        activityId: suggestion.activityId,
        propertyName: suggestion.propertyName,
        expectedCurrentValue: suggestion.currentValue,
        suggestedValue: suggestion.suggestedValue,
        expectedFileHash: suggestion.expectedFileHash,
        createBackup: true,
      }) as FixApplyResult;
      setApplyResult(result);
      setAnalysisOutOfDate(result.requiresReanalysis === true);
      await refreshBackups(projectPath.trim());
      setStatusMessage(result.message);
      setPendingApplyFix(null);
    } catch (error) {
      setStatusMessage(error instanceof Error ? error.message : t('applyFixFailed'));
    } finally {
      setIsApplyingFix(false);
    }
  }

  async function refreshBackups(path = projectPath.trim()) {
    if (!path) {
      setBackups([]);
      return;
    }

    setHistoryLoading(true);
    try {
      const result = await listBackups(path) as BackupListResult;
      setBackups(result.backups ?? []);
    } catch (error) {
      setStatusMessage(error instanceof Error ? error.message : 'Change history could not be loaded.');
    } finally {
      setHistoryLoading(false);
    }
  }

  async function refreshAnalysisHistory() {
    setHistoryLoading(true);
    try {
      const result = await listAnalysisHistory() as AnalysisHistoryList;
      setAnalysisSnapshots(result.snapshots ?? []);
    } catch (error) {
      setStatusMessage(error instanceof Error ? error.message : t('analysisHistoryLoadFailed'));
    } finally {
      setHistoryLoading(false);
    }
  }

  async function compareSnapshots(snapshot: AnalysisSnapshotSummary) {
    const comparisonProjectPath = snapshot.projectPath ?? projectPath.trim();
    if (!comparisonProjectPath || !snapshot.previousSnapshotId) {
      return;
    }

    setComparisonLoading(true);
    try {
      const result = await compareAnalysisSnapshots({
        projectPath: comparisonProjectPath,
        baselineSnapshotId: snapshot.previousSnapshotId,
        targetSnapshotId: snapshot.snapshotId,
      }) as AnalysisComparison;
      setSelectedComparison(result);
    } catch (error) {
      setStatusMessage(error instanceof Error ? error.message : t('analysisComparisonFailed'));
    } finally {
      setComparisonLoading(false);
    }
  }

  async function undoSelectedBackup(backup: BackupSummary) {
    if (!projectPath.trim() || !backup.workflowPath) {
      setStatusMessage('Undo request is missing project or workflow details.');
      return;
    }

    setIsUndoing(true);
    try {
      const result = await undoFix({
        projectPath: projectPath.trim(),
        backupId: backup.backupId,
        workflowPath: backup.workflowPath,
        expectedCurrentHash: backup.modifiedHash,
        createSafetyBackup: true,
      }) as UndoResult;
      setUndoResult(result);
      setAnalysisOutOfDate(result.requiresReanalysis === true);
      setStatusMessage(result.message);
      setPendingUndo(null);
      await refreshBackups(projectPath.trim());
    } catch (error) {
      setStatusMessage(error instanceof Error ? error.message : t('undoFailed'));
    } finally {
      setIsUndoing(false);
    }
  }

  async function askProjectFromUi(questionOverride?: string, preferredWorkflowPath?: string) {
    const question = (questionOverride ?? projectQuestion).trim();
    if (!projectPath.trim() || !analysis) {
      setStatusMessage(t('staticAnalysisBeforeAsk'));
      return;
    }

    if (!question) {
      setStatusMessage(t('enterProjectQuestion'));
      return;
    }

    setAskLoading(true);
    setActiveTab('ask');
    try {
      const answer = await askProject({
        projectPath: projectPath.trim(),
        profileId: selectedProfileId,
        preferredWorkflowPath,
        question,
        maxEvidenceItems: 20,
      }) as ProjectAnswer;
      setQuestionHistory((items) => [{ question, answer }, ...items].slice(0, 8));
      setProjectQuestion('');
      setStatusMessage(answer.usedAi ? t('aiAssistedAnswerCompleted') : t('answeredLocal'));
    } catch (error) {
      setStatusMessage(error instanceof Error ? error.message : t('askProjectFailed'));
    } finally {
      setAskLoading(false);
    }
  }

  async function runExport(format: ReportFormat) {
    if (!projectPath.trim() || !analysis) {
      setStatusMessage(t('analysisBeforeExport'));
      return;
    }

    setExportingFormat(format);
    try {
      const message = await exportReport({
        projectPath: projectPath.trim(),
        projectName: analysis.projectName,
        profileId: selectedProfileId,
        format,
        locale: language,
      });
      setStatusMessage(message);
    } catch {
      setStatusMessage(t('reportExportFailed', { format: format.toUpperCase() }));
    } finally {
      setExportingFormat(null);
    }
  }

  const navigationItems: Array<{ label: string; tab?: ActiveTab }> = [
    { label: t('home'), tab: 'overview' },
    { label: t('projects'), tab: 'overview' },
    { label: t('codeReview'), tab: 'workflows' },
    { label: t('findingsNav'), tab: 'findings' },
    { label: t('rules'), tab: 'rules' },
    { label: t('flowchartConverter'), tab: 'flowchartConverter' },
    { label: t('aiReview'), tab: 'ai' },
    { label: t('reports'), tab: 'report' },
    { label: t('reviewHistory'), tab: 'history' },
  ];

  return (
    <main className="app-shell">
      <aside className="sidebar" aria-label={t('primaryNavigation')}>
        <div className="brand-lockup">
          <span className="brand-mark">R</span>
          <strong>RPA Dev Assistant</strong>
        </div>
        <nav className="sidebar-nav">
          {navigationItems.map((item) => (
            <button
              key={item.label}
              type="button"
              aria-label={`Sidebar ${item.label}`}
              className={item.tab === activeTab ? 'active' : ''}
              onClick={() => item.tab && setActiveTab(item.tab)}
            >
              {item.label}
            </button>
          ))}
        </nav>
        <div className="sidebar-footer">
          <button type="button" onClick={() => setActiveTab('settings')}>{t('settings')}</button>
          <button type="button">{t('help')}</button>
          <div className="mini-profile">
            <span className="avatar">MD</span>
            <span>Mina Dilek</span>
          </div>
        </div>
      </aside>

      <section className="main-area">
        <header className="top-bar">
          <div>
            <h1>{analysis ? analysis.projectName ?? t('projectOverview') : t('dashboard')}</h1>
            <p>{analysis ? t('projectWorkspace') : t('homeSubtitle')}</p>
          </div>
          <div className="top-actions">
            <input aria-label="Global search" placeholder={t('globalSearch')} />
            <button className="language-switch" type="button" onClick={() => setLanguage(language === 'tr' ? 'en' : 'tr')}>
              {language.toUpperCase()} / {language === 'tr' ? 'EN' : 'TR'}
            </button>
            <button className="icon-button" type="button" aria-label="Notifications">
              <Bell size={18} />
            </button>
            <button className="icon-button" type="button" onClick={refreshHealth} aria-label="Refresh backend status">
              <RefreshCw size={18} />
            </button>
            <div className="avatar-menu-wrap">
              <button className="avatar-button" type="button" onClick={() => setAvatarMenuOpen((open) => !open)} aria-label="User menu">
                <span className="avatar">MD</span>
                <ChevronDown size={14} />
              </button>
              {avatarMenuOpen && (
                <div className="avatar-menu">
                  <button type="button" onClick={() => setActiveTab('settings')}>Profilim</button>
                  <button type="button" onClick={() => setActiveTab('settings')}>Tercihler</button>
                  <button type="button" onClick={() => setSettingsSection('languageSettings')}>{t('languageSettings')}</button>
                  <button type="button" onClick={() => setSettingsSection('appearance')}>{t('theme')}</button>
                  <button type="button">Çıkış Yap</button>
                </div>
              )}
            </div>
          </div>
        </header>

        <section className="workspace">
          <section className="project-card" aria-label="Project intake">
            <div className="project-card-header">
              <div>
                <span className="eyebrow">{t('projectIntake')}</span>
                <h2>{t('uipathProject')}</h2>
              </div>
              <span className={`health ${healthState}`}>{statusMessage}</span>
            </div>
            <div className="project-grid">
              <div className="field-group">
                <label htmlFor="projectPath">{t('uipathProject')}</label>
                <div className="path-row">
                  <input
                    id="projectPath"
                    value={projectPath}
                    onChange={(event) => {
                      setProjectPath(event.target.value);
                      setSelectedFolder(null);
                    }}
                    placeholder={t('selectProjectFirst')}
                  />
                  <button type="button" onClick={browseProjectFolder} title={desktop ? t('browseTitleDesktop') : t('browseTitleBrowser')}>
                    <FolderOpen size={18} />
                    {t('browse')}
                  </button>
                </div>
              </div>
              <div className="field-group profile-field">
                <label htmlFor="analysisProfile">{t('analysisProfile')}</label>
                <select id="analysisProfile" value={selectedProfileId} onChange={(event) => setSelectedProfileId(event.target.value)}>
                  {(profiles.length > 0 ? profiles : [{ id: 'default', name: 'Default', rules: [] }]).map((profile) => (
                    <option key={profile.id} value={profile.id}>{profile.name}</option>
                  ))}
                </select>
              </div>
            </div>
            {!desktop && <p className="hint">{t('browserModeHint')}</p>}
            <div className="validation-row">
              <span className={selectedFolder || projectPath.trim() ? 'status-dot ready' : 'status-dot'} />
              <span>{selectedFolder ? t('folderSelected') : projectPath.trim() ? t('manualPathEntered') : t('noFolderSelected')}</span>
            </div>
            <div className="action-row">
              <button className="primary-action" type="button" onClick={runAnalysis} disabled={isAnalyzing || healthState !== 'ready'}>
                <Play size={18} />
                {isAnalyzing ? t('analyzing') : t('analyzeProject')}
              </button>
              <button type="button" onClick={() => void runExport('json')} disabled={!analysis || exportingFormat !== null}>
                <FileJson size={18} />
                {exportingFormat === 'json' ? t('exporting') : t('exportJson')}
              </button>
              <button type="button" onClick={() => void runExport('html')} disabled={!analysis || exportingFormat !== null}>
                <Download size={18} />
                {exportingFormat === 'html' ? t('exporting') : t('exportHtml')}
              </button>
              <button type="button" onClick={() => void runExport('pdf')} disabled={!analysis || exportingFormat !== null}>
                <Download size={18} />
                {exportingFormat === 'pdf' ? t('exporting') : t('exportPdf')}
              </button>
              <button type="button" onClick={() => setReportModalOpen(true)} disabled={!analysis}>
                {t('createReport')}
              </button>
              <button type="button" onClick={() => void runAiReviewFromUi('Project')} disabled={!analysis || aiReviewLoading}>
                <Bot size={18} />
                {aiReviewLoading ? t('analyzingWithAi') : t('runAiProjectReview')}
              </button>
            </div>
          </section>

          {activeTab === 'settings' ? (
            <SettingsView section={settingsSection} language={language} t={t} onSectionChange={setSettingsSection} onLanguageChange={setLanguage} />
          ) : activeTab === 'flowchartConverter' ? (
            <FlowchartConverterView desktop={desktop} t={t} />
          ) : activeTab === 'rules' ? (
            <RulesView
              rules={rules}
              isLoading={rulesLoading}
              selectedRule={selectedRule}
              projectPath={projectPath.trim()}
              profiles={profiles}
              status={ruleStatus}
              t={t}
              onSelectRule={setSelectedRule}
              onRefresh={() => {
                void refreshRules();
                void refreshProfiles();
              }}
              onStatus={setRuleStatus}
            />
          ) : analysis ? (
            <>
              <nav className="tabs" aria-label="Analysis sections">
                {(['overview', 'findings', 'history', 'workflows', 'dependencies', 'rules', 'report', 'ai', 'ask'] as const).map((tab) => (
                  <button
                    key={tab}
                    type="button"
                    aria-label={tab === 'ai' ? 'AI' : undefined}
                    className={activeTab === tab ? 'active' : ''}
                    onClick={() => setActiveTab(tab)}
                  >
                    {tab === 'overview' ? t('overview')
                      : tab === 'findings' ? t('findingsNav')
                        : tab === 'history' ? t('changeHistory')
                            : tab === 'workflows' ? t('workflows')
                              : tab === 'dependencies' ? t('dependencies')
                                : tab === 'rules' ? t('rules')
                                  : tab === 'report' ? t('report')
                                    : tab === 'ai' ? t('aiReview')
                                      : t('askProject')}
                  </button>
                ))}
              </nav>
              <section className="results-panel" aria-label="Analysis results">
                {analysisOutOfDate && (
                  <div className="notice stale-analysis">
                    <strong>{t('analysisOutOfDate')}</strong> {t('analysisOutOfDateDetail')}
                    <button type="button" onClick={runAnalysis} disabled={isAnalyzing}>{t('rerunAnalysis')}</button>
                  </div>
                )}
                {activeTab === 'overview' && (
                  <Overview
                    analysis={analysis}
                    t={t}
                    onNavigate={setActiveTab}
                    onOpenWorkflow={(workflowPath) => {
                      const workflow = workflowHealth.find((item) => normalizeWorkflowPath(item.relativePath) === normalizeWorkflowPath(workflowPath));
                      if (workflow) {
                        setSelectedWorkflow(workflow);
                        setActiveTab('workflows');
                      }
                    }}
                  />
                )}
                {activeTab === 'findings' && (
                  <Findings
                    findings={filteredFindings}
                    allFindings={findings}
                    severityFilter={findingSeverityFilter}
                    categoryFilter={findingCategoryFilter}
                    categories={findingCategories}
                    onSeverityFilterChange={setFindingSeverityFilter}
                    onCategoryFilterChange={setFindingCategoryFilter}
                    fixResult={fixResult}
                    applyResult={applyResult}
                    selectedFixFinding={selectedFixFinding}
                    loadingKey={fixLoadingKey}
                    isApplyingFix={isApplyingFix}
                    locale={language}
                    t={t}
                    onFix={(finding, useAi) => void viewFixSuggestion(finding, useAi)}
                    onApply={(suggestion) => setPendingApplyFix(suggestion)}
                  />
                )}
                {activeTab === 'history' && (
                  <ChangeHistory
                    backups={backups}
                    snapshots={analysisSnapshots}
                    selectedComparison={selectedComparison}
                    comparisonLoading={comparisonLoading}
                    isLoading={historyLoading}
                    undoResult={undoResult}
                    t={t}
                    onRefresh={() => {
                      void refreshBackups();
                      void refreshAnalysisHistory();
                    }}
                    onCompare={compareSnapshots}
                    onUndo={(backup) => setPendingUndo(backup)}
                  />
                )}
                {activeTab === 'workflows' && (
                  <WorkflowsView
                    projectPath={projectPath}
                    workflows={filteredWorkflows}
                    workflowCount={workflowHealth.length}
                    query={workflowQuery}
                    typeFilter={workflowTypeFilter}
                    sort={workflowSort}
                    categories={workflowCategories}
                    selectedWorkflow={selectedWorkflow}
                    allWorkflows={workflowHealth}
                    findings={findings}
                    dependencyAnalysis={analysis.dependencyAnalysis ?? null}
                    locale={language}
                    t={t}
                    onQueryChange={setWorkflowQuery}
                    onTypeFilterChange={setWorkflowTypeFilter}
                    onSortChange={setWorkflowSort}
                    onSelectWorkflow={setSelectedWorkflow}
                    onReview={(workflowPath) => void runAiReviewFromUi('Workflow', workflowPath)}
                    isReviewing={aiReviewLoading}
                    onProjectChanged={() => {
                      setAnalysisOutOfDate(true);
                      void refreshBackups();
                    }}
                  />
                )}
                {activeTab === 'dependencies' && <DependenciesView dependencyAnalysis={analysis.dependencyAnalysis ?? null} findings={findings} locale={language} t={t} />}
                {activeTab === 'report' && <ReportView analysis={analysis} topIssues={topIssues} workflows={workflowHealth} locale={language} t={t} />}
                {activeTab === 'ai' && <AiReviewPanel result={aiReview} isLoading={aiReviewLoading} onAsk={(question) => void askProjectFromUi(question)} t={t} />}
                {activeTab === 'ask' && (
                  <AskProjectPanel
                    question={projectQuestion}
                    setQuestion={setProjectQuestion}
                    history={questionHistory}
                    isLoading={askLoading}
                    t={t}
                    onAsk={(question) => void askProjectFromUi(question)}
                  />
                )}
              </section>
            </>
          ) : (
            <DashboardEmpty t={t} onAnalyze={() => void runAnalysis()} onOpenProject={() => document.getElementById('projectPath')?.focus()} />
          )}
        </section>
      </section>
      {pendingApplyFix && (
        <ApplyFixDialog
          suggestion={pendingApplyFix}
          isApplying={isApplyingFix}
          t={t}
          onCancel={() => setPendingApplyFix(null)}
          onConfirm={() => void applySelectedFix(pendingApplyFix)}
        />
      )}
      {pendingUndo && (
        <UndoDialog
          backup={pendingUndo}
          isUndoing={isUndoing}
          t={t}
          onCancel={() => setPendingUndo(null)}
          onConfirm={() => void undoSelectedBackup(pendingUndo)}
        />
      )}
      {reportModalOpen && <ReportCreateDialog t={t} onCancel={() => setReportModalOpen(false)} onExport={(format) => { setReportModalOpen(false); void runExport(format); }} />}
    </main>
  );
}

function Overview({
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

function FlowchartConverterView({
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
  const [saveResult, setSaveResult] = React.useState<StandaloneFlowchartConvertResult | null>(null);
  const [outputPath, setOutputPath] = React.useState('');
  const selected = results.find((item) => item.filePath === selectedPath) ?? results[0] ?? null;
  const selectedFlowchartNodes = selected?.graph?.nodes ?? [];
  const selectedHasFlowchart = selectedFlowchartNodes.length > 0;

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
      setMessage(desktop ? null : t('standaloneBrowserModeHint'));
      return;
    }

    setXamlPath(paths[0]);
    await analyzePaths(paths);
  }

  async function analyzeManualPath() {
    const path = xamlPath.trim();
    if (!path) {
      setMessage(t('xamlPathRequired'));
      return;
    }

    await analyzePaths([path]);
  }

  async function analyzePaths(paths: string[]) {
    setIsAnalyzing(true);
    setMessage(null);
    setSaveResult(null);
    try {
      const analyzed = await Promise.all(paths.map((path) => analyzeStandaloneFlowchart({ xamlFilePath: path }) as Promise<StandaloneFlowchartAnalysisResult>));
      setResults((current) => mergeStandaloneResults(current, analyzed));
      setSelectedPath(analyzed[0]?.filePath ?? null);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : t('standaloneFlowchartAnalyzeFailed'));
    } finally {
      setIsAnalyzing(false);
    }
  }

  async function chooseOutputPath() {
    if (!selected?.canConvert) {
      return null;
    }

    const suggested = outputPath.trim() || defaultConvertedPath(selected.filePath, selected.suggestedOutputFileName);
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
      targetPath = await chooseOutputPath() ?? '';
      if (!targetPath) {
        setMessage(t('outputXamlPathRequired'));
        return;
      }
    }

    setIsSaving(true);
    setMessage(null);
    setSaveResult(null);
    try {
      const result = await convertStandaloneFlowchart({
        xamlFilePath: selected.filePath,
        outputPath: targetPath,
        expectedWorkflowHash: selected.workflowHash,
        confirmed: true,
      }) as StandaloneFlowchartConvertResult;
      setSaveResult(result);
      setMessage(result.message);
      setResults((current) => current.map((item) => item.filePath === selected.filePath ? { ...item, status: 'Converted' } : item));
    } catch (error) {
      setMessage(error instanceof Error ? error.message : t('standaloneFlowchartConvertFailed'));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="results-panel flowchart-converter-view" aria-label={t('flowchartConverter')}>
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t('standaloneTool')}</span>
          <h2>{t('flowchartConverter')}</h2>
          <p>{t('flowchartConverterHelp')}</p>
        </div>
      </div>

      <div className="project-grid">
        <div className="field-group">
          <label htmlFor="standaloneXamlPath">{t('xamlFilePath')}</label>
          <div className="path-row">
            <input id="standaloneXamlPath" value={xamlPath} onChange={(event) => setXamlPath(event.target.value)} placeholder={t('selectXamlFirst')} />
            <button type="button" onClick={() => void pickFiles()} title={desktop ? t('selectXamlFile') : t('browseTitleBrowser')}>
              <FolderOpen size={18} />
              {t('selectXamlFile')}
            </button>
          </div>
        </div>
        <div className="field-group">
          <label>{t('standaloneContext')}</label>
          <div className="validation-row">
            <span className={xamlPath.trim() ? 'status-dot ready' : 'status-dot'} />
            <span>{xamlPath.trim() ? t('xamlFileSelected') : t('noXamlFileSelected')}</span>
          </div>
        </div>
      </div>
      {!desktop && <p className="hint">{t('standaloneBrowserModeHint')}</p>}
      <div className="action-row">
        <button className="primary-action" type="button" onClick={() => void analyzeManualPath()} disabled={isAnalyzing}>
          <GitBranch size={18} />
          {isAnalyzing ? t('analyzing') : t('analyzeXaml')}
        </button>
      </div>
      {message && <p className={saveResult?.success ? 'success-text' : 'error-text'}>{message}</p>}

      <div className={`workflows-layout ${selected ? 'with-drawer' : 'single-column'}`}>
        <section className="list-panel">
          <h3>{t('selectedXamlFiles')}</h3>
          {results.length === 0 ? <p className="empty-state">{t('flowchartConverterEmpty')}</p> : (
            <table className="clickable-table">
              <thead>
                <tr>
                  <th>{t('fileName')}</th>
                  <th>{t('structureType')}</th>
                  <th>{t('status')}</th>
                  <th>{t('activityCount')}</th>
                  <th>{t('nodes')}</th>
                  <th>{t('decisions')}</th>
                </tr>
              </thead>
              <tbody>
                {results.map((result) => (
                  <tr key={result.filePath} onClick={() => setSelectedPath(result.filePath)}>
                    <td>{result.fileName}</td>
                    <td>{result.structureType}</td>
                    <td><span className={`status-badge ${flowchartStandaloneStatusClass(result.status)}`}>{localizeStandaloneFlowchartStatus(result.status, t)}</span></td>
                    <td>{result.activityCount ?? 0}</td>
                    <td>{result.flowchartNodeCount ?? result.graph?.nodes.length ?? 0}</td>
                    <td>{result.decisionCount ?? result.graph?.decisions.length ?? 0}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>

        {selected && (
          <aside className="detail-drawer" aria-label={t('flowchartConverterDetail')}>
            <div className="section-header">
              <div>
                <span className="eyebrow">{t('workflowDetail')}</span>
                <h2>{selected.fileName}</h2>
              </div>
            </div>
            <div className="metric-grid compact">
              <Metric label={t('xamlFilePath')} value={selected.filePath} />
              <Metric label={t('currentStructure')} value={selected.structureType} />
              <Metric label={t('status')} value={localizeStandaloneFlowchartStatus(selected.status, t)} />
              <Metric label={t('arguments')} value={selected.argumentCount ?? 0} />
              <Metric label={t('nodes')} value={selected.flowchartNodeCount ?? selected.graph?.nodes.length ?? 0} />
              <Metric label={t('switches')} value={selected.switchCount ?? selected.graph?.switches.length ?? 0} />
            </div>
            <p className="empty-state">{t('originalFileNotModified')}</p>
            {!selected.canConvert && (
              <div className="notice conversion-unavailable">
                <strong>{t('conversionNotAvailable')}</strong>
                <p>{t(selectedHasFlowchart ? 'conversionNotAvailableNestedReason' : 'conversionNotAvailableReason')}</p>
                {(selected.messages?.length ?? 0) > 0 && <ul>{selected.messages?.map((item) => <li key={item}>{item}</li>)}</ul>}
              </div>
            )}
            {selectedHasFlowchart && !selected.assessment && (
              <div className="conversion-panel">
                <section>
                  <h3>{t('detectedFlowchartNodes')}</h3>
                  <ul className="compact-list">
                    {selectedFlowchartNodes.slice(0, 12).map((node) => (
                      <li key={node.id}>{node.id} · {node.type} · {node.displayName ?? node.activityName ?? '-'}</li>
                    ))}
                  </ul>
                </section>
              </div>
            )}
            {selected.assessment && (
              <div className="conversion-panel">
                <div className="metric-grid compact">
                  <Metric label={t('convertibility')} value={localizeFlowchartLevel(selected.assessment.conversionLevel, t)} />
                  <Metric label={t('confidence')} value={localizeFlowchartConfidence(selected.assessment.confidence, t)} />
                  <Metric label={t('cycles')} value={selected.graph?.hasCycles ? t('yes') : t('no')} />
                  <Metric label={t('unreachableNodes')} value={selected.graph?.hasUnreachableNodes ? t('yes') : t('no')} />
                </div>
                <div className="before-after-grid">
                  <section>
                    <h3>{t('currentFlowchart')}</h3>
                    <ul className="compact-list">
                      {(selected.graph?.nodes ?? []).slice(0, 12).map((node) => (
                        <li key={node.id}>{node.id} · {node.type} · {node.displayName ?? node.activityName ?? '-'}</li>
                      ))}
                    </ul>
                  </section>
                  <section>
                    <h3>{t('proposedSequence')}</h3>
                    {selected.plan?.previewTree ? <PreviewTree node={selected.plan.previewTree} /> : <p className="empty-state">{t('noPreviewAvailable')}</p>}
                  </section>
                </div>
                <details open>
                  <summary>{t('steps')}</summary>
                  <ul>{selected.plan?.steps.map((step) => <li key={step}>{step}</li>)}</ul>
                </details>
                <details open>
                  <summary>{t('risks')}</summary>
                  {(selected.assessment.risks.length ?? 0) === 0 ? <p className="empty-state">{t('noRisks')}</p> : (
                    <ul>{selected.assessment.risks.map((risk) => <li key={risk}>{risk}</li>)}</ul>
                  )}
                </details>
                <details>
                  <summary>{t('conversionMappings')}</summary>
                  <table className="compact-table">
                    <thead><tr><th>{t('sourceNode')}</th><th>{t('targetPath')}</th><th>{t('type')}</th></tr></thead>
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
                </details>
              </div>
            )}
            <div className="conversion-actions">
              {selected.canConvert ? (
                <div className="field-group conversion-output-field">
                  <label htmlFor="standaloneOutputPath">{t('outputXamlPath')}</label>
                  <div className="path-row">
                    <input id="standaloneOutputPath" value={outputPath} onChange={(event) => setOutputPath(event.target.value)} placeholder={t('chooseOutputPathFirst')} />
                    <button type="button" onClick={() => void chooseOutputPath()}>
                      <FolderOpen size={18} />
                      {t('chooseOutputPath')}
                    </button>
                  </div>
                  <button className="primary-action" type="button" onClick={() => void saveConvertedWorkflow()} disabled={isSaving}>
                    <Download size={18} />
                    {isSaving ? t('saving') : t('convertAndSave')}
                  </button>
                </div>
              ) : (
                <span className="status-badge review">{t('manualReviewRequired')}</span>
              )}
            </div>
            {saveResult && (
              <div className={`notice ${saveResult.success ? 'success' : 'error'}`}>
                <strong>{saveResult.success ? t('convertedWorkflowSaved') : t('conversionWasNotApplied')}</strong>
                <p>{saveResult.message}</p>
                {saveResult.outputPath && <p>{t('outputXamlPath')}: {saveResult.outputPath}</p>}
                <p>{t('currentStructure')}: {saveResult.originalStructure} → {saveResult.newStructure}</p>
              </div>
            )}
          </aside>
        )}
      </div>
    </section>
  );
}

function DashboardEmpty({ t, onAnalyze, onOpenProject }: { t: (key: string, values?: Record<string, unknown>) => string; onAnalyze: () => void; onOpenProject: () => void }) {
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

function RulesView({
  rules,
  isLoading,
  selectedRule,
  projectPath,
  profiles,
  status,
  t,
  onSelectRule,
  onRefresh,
  onStatus,
}: {
  rules: RuleCatalogItem[];
  isLoading: boolean;
  selectedRule: RuleCatalogItem | null;
  projectPath: string;
  profiles: RuleProfile[];
  status: string;
  t: (key: string, values?: Record<string, unknown>) => string;
  onSelectRule: (rule: RuleCatalogItem) => void;
  onRefresh: () => void;
  onStatus: (message: string) => void;
}) {
  const [query, setQuery] = React.useState('');
  const [category, setCategory] = React.useState('All');
  const [severity, setSeverity] = React.useState('All');
  const [scope, setScope] = React.useState('All');
  const [source, setSource] = React.useState('All');
  const [enabled, setEnabled] = React.useState('All');
  const [draft, setDraft] = React.useState<CustomRuleDefinition>(() => createDefaultCustomRule());
  const [profileDraft, setProfileDraft] = React.useState<RuleProfile>(() => createDefaultRuleProfile(rules));
  const [testResult, setTestResult] = React.useState<CustomRuleTestResult | null>(null);
  const [isTesting, setIsTesting] = React.useState(false);
  const [isSaving, setIsSaving] = React.useState(false);
  const fileInputRef = React.useRef<HTMLInputElement | null>(null);
  const categories = unique(['All', ...rules.map((rule) => rule.category)]);
  const severities = unique(['All', ...rules.map((rule) => rule.defaultSeverity)]);
  const scopes = unique(['All', ...rules.map((rule) => rule.scope)]);
  const filtered = rules.filter((rule) => {
    const q = query.trim().toLowerCase();
    return (!q || rule.id.toLowerCase().includes(q) || rule.name.toLowerCase().includes(q))
      && (category === 'All' || rule.category === category)
      && (severity === 'All' || rule.defaultSeverity === severity)
      && (scope === 'All' || rule.scope === scope)
      && (source === 'All' || (source === 'BuiltIn' ? rule.isBuiltIn : rule.isCustom))
      && (enabled === 'All' || String(Boolean(rule.enabledByDefault)) === enabled);
  });

  React.useEffect(() => {
    setProfileDraft((current) => current.rules.length === 0 ? createDefaultRuleProfile(rules) : current);
  }, [rules]);

  async function runTestRule() {
    if (!projectPath) {
      onStatus(t('customRuleNeedsProject'));
      return;
    }

    setIsTesting(true);
    try {
      const result = (await testCustomRule(projectPath, draft)) as CustomRuleTestResult;
      setTestResult(result);
      onStatus(result.hasNoiseWarning ? result.noiseWarning ?? t('ruleNoiseWarning') : t('customRuleTestCompleted'));
    } catch (error) {
      onStatus(error instanceof Error ? error.message : t('customRuleTestFailed'));
    } finally {
      setIsTesting(false);
    }
  }

  async function saveRule() {
    setIsSaving(true);
    try {
      await saveCustomRule(draft);
      onStatus(t('customRuleSaved'));
      setDraft(createDefaultCustomRule());
      setTestResult(null);
      onRefresh();
    } catch (error) {
      onStatus(error instanceof Error ? error.message : t('customRuleSaveFailed'));
    } finally {
      setIsSaving(false);
    }
  }

  async function downloadRules() {
    const store = await exportCustomRules();
    downloadJson('custom-rules.json', store);
    onStatus(t('customRulesExported'));
  }

  async function importRulesFromPrompt() {
    fileInputRef.current?.click();
  }

  async function importRulesFromFile(file: File) {
    try {
      const raw = await file.text();
      const parsed = JSON.parse(raw) as { rules?: unknown[] } | unknown[];
      const rulesToImport = Array.isArray(parsed) ? parsed : parsed.rules ?? [];
      await importCustomRules(rulesToImport, false);
      onStatus(t('customRulesImported'));
      onRefresh();
    } catch {
      onStatus(t('customRulesImportFailed'));
    }
  }

  async function saveProfile() {
    try {
      await saveRuleProfile(profileDraft);
      onStatus(t('ruleProfileSaved'));
      onRefresh();
    } catch (error) {
      onStatus(error instanceof Error ? error.message : t('ruleProfileSaveFailed'));
    }
  }

  async function downloadProfiles() {
    const store = await exportRuleProfiles();
    downloadJson('rule-profiles.json', store);
    onStatus(t('ruleProfilesExported'));
  }

  return (
    <section className="results-panel rules-panel" aria-label={t('rules')}>
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t('ruleCatalog')}</span>
          <h2>{t('rules')}</h2>
        </div>
        <div className="action-row compact-actions">
          <button type="button" onClick={onRefresh}>{t('refresh')}</button>
          <button type="button" onClick={() => void downloadRules()}>{t('exportRules')}</button>
          <button type="button" onClick={() => void importRulesFromPrompt()}>{t('importRules')}</button>
          <input
            ref={fileInputRef}
            type="file"
            accept="application/json,.json"
            hidden
            onChange={(event) => {
              const file = event.target.files?.[0];
              event.target.value = '';
              if (file) {
                void importRulesFromFile(file);
              }
            }}
          />
        </div>
      </div>
      {status && <p className="hint">{status}</p>}
      <div className="filter-row">
        <input aria-label={t('searchRules')} placeholder={t('searchRules')} value={query} onChange={(event) => setQuery(event.target.value)} />
        <select aria-label={t('category')} value={category} onChange={(event) => setCategory(event.target.value)}>{categories.map((item) => <option key={item}>{item}</option>)}</select>
        <select aria-label={t('severity')} value={severity} onChange={(event) => setSeverity(event.target.value)}>{severities.map((item) => <option key={item}>{item}</option>)}</select>
        <select aria-label={t('scope')} value={scope} onChange={(event) => setScope(event.target.value)}>{scopes.map((item) => <option key={item}>{item}</option>)}</select>
        <select aria-label={t('source')} value={source} onChange={(event) => setSource(event.target.value)}><option>All</option><option>BuiltIn</option><option>Custom</option></select>
        <select aria-label={t('enabled')} value={enabled} onChange={(event) => setEnabled(event.target.value)}><option value="All">All</option><option value="true">{t('enabled')}</option><option value="false">{t('disabled')}</option></select>
      </div>
      <div className="split-layout">
        <div>
          {isLoading ? <p className="empty-state">{t('loadingRules')}</p> : (
            <table className="compact-table">
              <thead><tr><th>{t('ruleId')}</th><th>{t('name')}</th><th>{t('category')}</th><th>{t('severity')}</th><th>{t('scope')}</th><th>{t('enabled')}</th><th>{t('source')}</th><th>{t('fixSupport')}</th></tr></thead>
              <tbody>
                {filtered.map((rule) => (
                  <tr key={rule.id} onClick={() => onSelectRule(rule)} className={selectedRule?.id === rule.id ? 'selected-row' : ''}>
                    <td><button type="button" className="link-button" onClick={() => onSelectRule(rule)}>{rule.id}</button></td>
                    <td>{rule.name}</td><td>{rule.category}</td><td>{rule.defaultSeverity}</td><td>{rule.scope}</td>
                    <td>{rule.enabledByDefault ? t('yes') : t('no')}</td>
                    <td>{rule.isBuiltIn ? t('builtIn') : t('custom')}</td>
                    <td>{rule.hasFixSuggestion ? t('yes') : t('no')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
        <aside className="detail-panel">
          {selectedRule ? (
            <>
              <span className="eyebrow">{selectedRule.isBuiltIn ? t('builtInRule') : t('customRule')}</span>
              <h3>{selectedRule.id} {selectedRule.name}</h3>
              <p>{selectedRule.description}</p>
              {selectedRule.recommendation && <p className="hint">{selectedRule.recommendation}</p>}
              <div className="metric-grid compact">
                <Metric label={t('category')} value={selectedRule.category} />
                <Metric label={t('severity')} value={selectedRule.defaultSeverity} />
                <Metric label={t('scope')} value={selectedRule.scope} />
                <Metric label={t('weight')} value={selectedRule.defaultWeight ?? 0} />
                <Metric label={t('maxPenalty')} value={selectedRule.defaultMaxPenalty ?? 0} />
                <Metric label={t('aggregationSupport')} value={selectedRule.supportsAggregation ? t('yes') : t('no')} />
                <Metric label={t('fixSupport')} value={selectedRule.hasFixSuggestion ? t('yes') : t('no')} />
                <Metric label={t('autoApply')} value={selectedRule.canAutoApply ? t('yes') : t('no')} />
              </div>
              <pre className="preview-block">{selectedRule.defaultSeverity} · {selectedRule.category} · {selectedRule.scope}</pre>
            </>
          ) : <p className="empty-state">{t('selectRule')}</p>}
        </aside>
      </div>
      <RuleProfileBuilder
        profiles={profiles}
        rules={rules}
        draft={profileDraft}
        setDraft={setProfileDraft}
        t={t}
        onSave={() => void saveProfile()}
        onExport={() => void downloadProfiles()}
      />
      <CustomRuleBuilder draft={draft} setDraft={setDraft} testResult={testResult} isTesting={isTesting} isSaving={isSaving} t={t} onTest={() => void runTestRule()} onSave={() => void saveRule()} />
    </section>
  );
}

function RuleProfileBuilder({
  profiles,
  rules,
  draft,
  setDraft,
  t,
  onSave,
  onExport,
}: {
  profiles: RuleProfile[];
  rules: RuleCatalogItem[];
  draft: RuleProfile;
  setDraft: React.Dispatch<React.SetStateAction<RuleProfile>>;
  t: (key: string, values?: Record<string, unknown>) => string;
  onSave: () => void;
  onExport: () => void;
}) {
  function updateRule(ruleId: string, patch: Partial<RuleProfile['rules'][number]>) {
    setDraft((profile) => ({
      ...profile,
      rules: profile.rules.map((rule) => rule.ruleId === ruleId ? { ...rule, ...patch } : rule),
    }));
  }

  return (
    <section className="builder-panel">
      <div className="section-heading">
        <div><span className="eyebrow">{t('customRuleProfiles')}</span><h2>{t('profileBuilder')}</h2></div>
        <div className="action-row compact-actions">
          <button type="button" onClick={onExport}>{t('exportProfiles')}</button>
          <button className="primary-action" type="button" onClick={onSave}>{t('saveProfile')}</button>
        </div>
      </div>
      <div className="builder-grid">
        <label>{t('profileId')}<input value={draft.id} onChange={(event) => setDraft((profile) => ({ ...profile, id: event.target.value }))} /></label>
        <label>{t('profileName')}<input value={draft.name} onChange={(event) => setDraft((profile) => ({ ...profile, name: event.target.value }))} /></label>
        <label className="wide-field">{t('description')}<textarea value={draft.description ?? ''} onChange={(event) => setDraft((profile) => ({ ...profile, description: event.target.value }))} /></label>
        <label className="wide-field">{t('profileTemplate')}
          <select
            value=""
            onChange={(event) => {
              const selected = profiles.find((profile) => profile.id === event.target.value);
              if (selected) {
                setDraft({
                  ...selected,
                  id: selected.id === 'default' ? `custom-${Date.now()}` : selected.id,
                  name: selected.id === 'default' ? `${selected.name} Custom` : selected.name,
                });
              }
            }}
          >
            <option value="">{t('selectProfileTemplate')}</option>
            {profiles.map((profile) => <option key={profile.id} value={profile.id}>{profile.name}</option>)}
          </select>
        </label>
      </div>
      <div className="table-scroll">
        <table className="compact-table">
          <thead><tr><th>{t('ruleId')}</th><th>{t('enabled')}</th><th>{t('severityOverride')}</th><th>{t('weight')}</th><th>{t('maxPenalty')}</th></tr></thead>
          <tbody>
            {draft.rules.map((rule) => {
              const catalogRule = rules.find((item) => item.id === rule.ruleId);
              return (
                <tr key={rule.ruleId}>
                  <td>{rule.ruleId} {catalogRule?.name ?? rule.description}</td>
                  <td><input type="checkbox" checked={rule.enabled} onChange={(event) => updateRule(rule.ruleId, { enabled: event.target.checked })} /></td>
                  <td>
                    <select value={rule.severityOverride ?? ''} onChange={(event) => updateRule(rule.ruleId, { severityOverride: event.target.value || null })}>
                      <option value="">{t('defaultSeverity')}</option>
                      {ruleSeverities().map((item) => <option key={item}>{item}</option>)}
                    </select>
                  </td>
                  <td><input type="number" min="0" value={rule.weight} onChange={(event) => updateRule(rule.ruleId, { weight: Number(event.target.value) })} /></td>
                  <td><input type="number" min="0" value={rule.maxPenalty} onChange={(event) => updateRule(rule.ruleId, { maxPenalty: Number(event.target.value) })} /></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function CustomRuleBuilder({
  draft,
  setDraft,
  testResult,
  isTesting,
  isSaving,
  t,
  onTest,
  onSave,
}: {
  draft: CustomRuleDefinition;
  setDraft: React.Dispatch<React.SetStateAction<CustomRuleDefinition>>;
  testResult: CustomRuleTestResult | null;
  isTesting: boolean;
  isSaving: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
  onTest: () => void;
  onSave: () => void;
}) {
  return (
    <section className="builder-panel">
      <div className="section-heading">
        <div><span className="eyebrow">{t('customRuleBuilder')}</span><h2>{t('createRule')}</h2></div>
        <div className="action-row compact-actions">
          <button type="button" onClick={onTest} disabled={isTesting}>{isTesting ? t('testing') : t('testRule')}</button>
          <button className="primary-action" type="button" onClick={onSave} disabled={isSaving}>{isSaving ? t('saving') : t('saveRule')}</button>
        </div>
      </div>
      <div className="builder-grid">
        <label>{t('ruleId')}<input value={draft.id} onChange={(event) => setDraft((rule) => ({ ...rule, id: event.target.value }))} /></label>
        <label>{t('ruleName')}<input value={draft.name} onChange={(event) => setDraft((rule) => ({ ...rule, name: event.target.value }))} /></label>
        <label>{t('ruleNameEn')}<input value={draft.nameEn ?? ''} onChange={(event) => setDraft((rule) => ({ ...rule, nameEn: event.target.value }))} /></label>
        <label>{t('ruleNameTr')}<input value={draft.nameTr ?? ''} onChange={(event) => setDraft((rule) => ({ ...rule, nameTr: event.target.value }))} /></label>
        <label>{t('category')}<select value={draft.category} onChange={(event) => setDraft((rule) => ({ ...rule, category: event.target.value }))}>{ruleCategories().map((item) => <option key={item}>{item}</option>)}</select></label>
        <label>{t('severity')}<select value={draft.severity} onChange={(event) => setDraft((rule) => ({ ...rule, severity: event.target.value }))}>{ruleSeverities().map((item) => <option key={item}>{item}</option>)}</select></label>
        <label>{t('scope')}<select value={draft.scope} onChange={(event) => setDraft((rule) => ({ ...rule, scope: event.target.value }))}>{ruleScopes().map((item) => <option key={item}>{item}</option>)}</select></label>
        <label>{t('matchMode')}<select value={draft.matchMode} onChange={(event) => setDraft((rule) => ({ ...rule, matchMode: event.target.value }))}><option>All</option><option>Any</option></select></label>
        <label>{t('weight')}<input type="number" min="0" value={draft.weight} onChange={(event) => setDraft((rule) => ({ ...rule, weight: Number(event.target.value) }))} /></label>
        <label>{t('maxPenalty')}<input type="number" min="0" value={draft.maxPenalty} onChange={(event) => setDraft((rule) => ({ ...rule, maxPenalty: Number(event.target.value) }))} /></label>
        <label className="wide-field">{t('description')}<textarea value={draft.description ?? ''} onChange={(event) => setDraft((rule) => ({ ...rule, description: event.target.value }))} /></label>
        <label className="wide-field">{t('descriptionEn')}<textarea value={draft.descriptionEn ?? ''} onChange={(event) => setDraft((rule) => ({ ...rule, descriptionEn: event.target.value }))} /></label>
        <label className="wide-field">{t('descriptionTr')}<textarea value={draft.descriptionTr ?? ''} onChange={(event) => setDraft((rule) => ({ ...rule, descriptionTr: event.target.value }))} /></label>
        <label className="wide-field">{t('recommendation')}<textarea value={draft.recommendation ?? ''} onChange={(event) => setDraft((rule) => ({ ...rule, recommendation: event.target.value }))} /></label>
        <label className="wide-field">{t('recommendationEn')}<textarea value={draft.recommendationEn ?? ''} onChange={(event) => setDraft((rule) => ({ ...rule, recommendationEn: event.target.value }))} /></label>
        <label className="wide-field">{t('recommendationTr')}<textarea value={draft.recommendationTr ?? ''} onChange={(event) => setDraft((rule) => ({ ...rule, recommendationTr: event.target.value }))} /></label>
      </div>
      <div className="conditions-list">
        <h3>{t('conditions')}</h3>
        {draft.conditions.map((condition, index) => (
          <div className="condition-row" key={index}>
            <select value={condition.field} onChange={(event) => updateCondition(setDraft, index, { field: event.target.value })}>{conditionFields().map((item) => <option key={item}>{item}</option>)}</select>
            <select value={condition.operator} onChange={(event) => updateCondition(setDraft, index, { operator: event.target.value })}>{conditionOperators().map((item) => <option key={item}>{item}</option>)}</select>
            <input value={condition.propertyName ?? ''} onChange={(event) => updateCondition(setDraft, index, { propertyName: event.target.value })} placeholder={t('propertyName')} disabled={condition.field !== 'Activity.Property'} />
            <input value={condition.value ?? ''} onChange={(event) => updateCondition(setDraft, index, { value: event.target.value })} placeholder={t('value')} />
            <input value={condition.compareValue ?? ''} onChange={(event) => updateCondition(setDraft, index, { compareValue: event.target.value })} placeholder={t('compareValue')} disabled={condition.field !== 'Activity.Property'} />
            <label className="checkbox-label"><input type="checkbox" checked={Boolean(condition.caseSensitive)} onChange={(event) => updateCondition(setDraft, index, { caseSensitive: event.target.checked })} />{t('caseSensitive')}</label>
            <button type="button" onClick={() => removeCondition(setDraft, index)}>{t('remove')}</button>
          </div>
        ))}
        <button type="button" onClick={() => setDraft((rule) => ({ ...rule, conditions: [...rule.conditions, { field: 'Activity.Name', operator: 'Equals', value: '', caseSensitive: false }] }))}>{t('addCondition')}</button>
      </div>
      {testResult && (
        <div className="test-result">
          <h3>{t('testRuleResult')}</h3>
          <p>{t('customRuleMatches', { activities: testResult.matchedActivityCount, workflows: testResult.matchedWorkflowCount, findings: testResult.estimatedFindingCount })}</p>
          {testResult.hasNoiseWarning && <p className="notice">{testResult.noiseWarning}</p>}
          {(testResult.matchedWorkflows ?? []).slice(0, 10).map((workflow) => <span className="workflow-chip" key={workflow}>{workflow}</span>)}
        </div>
      )}
    </section>
  );
}

function ReportView({ analysis, topIssues, workflows, locale, t }: { analysis: AnalysisResponse; topIssues: ReturnType<typeof getTopIssues>; workflows: ReturnType<typeof getWorkflowHealth>; locale: Locale; t: (key: string, values?: Record<string, unknown>) => string }) {
  return (
    <div className="report-view">
      <Overview analysis={analysis} t={t} />
      <section>
        <h2>{t('summary')}</h2>
        <p>{analysis.analysis?.errorCount ?? 0} {t('errors')} · {analysis.analysis?.warningCount ?? 0} {t('warnings')} · {analysis.analysis?.suggestionCount ?? 0} {t('suggestions')}</p>
        <p>{t('profile')}: {analysis.qualityScore?.profileName ?? 'Default'}</p>
      </section>
      <section>
        <h2>{t('topIssues')}</h2>
        {topIssues.length === 0 ? <p>{t('noIssuesDetected')}</p> : topIssues.map((finding) => <FindingRow key={`${finding.ruleId}-${finding.workflowPath}-${finding.activityDisplayName}-${finding.message}`} finding={finding} locale={locale} t={t} />)}
      </section>
      <section>
        <h2>{t('workflowHealth')}</h2>
        <WorkflowHealth workflows={workflows.slice(0, 8)} t={t} />
      </section>
      <section>
        <h2>{t('whyThisScore')}</h2>
        <div className="metric-grid compact">
          <Metric label={t('rawPenalty')} value={analysis.qualityScore?.rawPenalty ?? 0} />
          <Metric label={t('normalizedPenalty')} value={analysis.qualityScore?.normalizedPenalty ?? 0} />
          <Metric label={t('projectSizeFactor')} value={analysis.qualityScore?.projectSizeFactor ?? 1} />
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
                  <td>{item.ruleId} {item.ruleName}</td>
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

function Findings({
  findings,
  allFindings,
  severityFilter,
  categoryFilter,
  categories,
  onSeverityFilterChange,
  onCategoryFilterChange,
  fixResult,
  applyResult,
  selectedFixFinding,
  loadingKey,
  isApplyingFix,
  locale,
  t,
  onFix,
  onApply,
}: {
  findings: ReturnType<typeof getTopIssues>;
  allFindings: Finding[];
  severityFilter: string;
  categoryFilter: string;
  categories: string[];
  onSeverityFilterChange: (value: string) => void;
  onCategoryFilterChange: (value: string) => void;
  fixResult?: FixSuggestionResult | null;
  applyResult?: FixApplyResult | null;
  selectedFixFinding?: Finding | null;
  loadingKey?: string | null;
  isApplyingFix?: boolean;
  locale: Locale;
  t: (key: string, values?: Record<string, unknown>) => string;
  onFix?: (finding: Finding, useAi?: boolean) => void;
  onApply?: (suggestion: FixSuggestion) => void;
}) {
  return (
    <div className="findings-view">
      <div className="filter-bar">
        {['All', 'Critical', 'Error', 'Warning', 'Suggestion', 'Info'].map((severity) => (
          <button key={severity} type="button" className={severityFilter === severity ? 'active' : ''} onClick={() => onSeverityFilterChange(severity)}>
            {severity === 'All' ? t('all') : severity}
          </button>
        ))}
        <select aria-label="Finding category" value={categoryFilter} onChange={(event) => onCategoryFilterChange(event.target.value)}>
          {categories.map((category) => <option key={category} value={category}>{category === 'All' ? t('category') : category}</option>)}
        </select>
        <span className="hint">{t('showingFindings', { shown: findings.length, total: allFindings.length })}</span>
      </div>
      {fixResult && (
        <FixSuggestionPanel
          result={fixResult}
          applyResult={applyResult}
          isApplyingFix={isApplyingFix}
          t={t}
          onApply={onApply}
          onGenerateAi={onFix && selectedFixFinding ? () => onFix(selectedFixFinding, true) : undefined}
        />
      )}
      {findings.length === 0 ? <p className="empty-state">{t('noFilteredFindings')}</p> : findings.map((finding) => {
        const key = `${finding.ruleId}-${finding.workflowPath ?? ''}-${finding.activityId ?? ''}-false`;
        return (
          <FindingRow
            key={`${finding.ruleId}-${finding.workflowPath}-${finding.activityDisplayName}-${finding.message}`}
            finding={finding}
            locale={locale}
            t={t}
            onFix={onFix}
            isFixLoading={loadingKey === key}
          />
        );
      })}
    </div>
  );
}

function FindingRow({ finding, locale = 'en', t = (key, values) => translate('en', key, values), onFix, isFixLoading = false }: { finding: ReturnType<typeof getTopIssues>[number]; locale?: Locale; t?: (key: string, values?: Record<string, unknown>) => string; onFix?: (finding: Finding, useAi?: boolean) => void; isFixLoading?: boolean }) {
  const displayFinding = localizeFinding(finding, locale);
  const isAggregated = finding.scope === 'Aggregated' || (finding.affectedActivityCount ?? 0) > 0;
  const examples = finding.exampleActivities ?? [];
  return (
    <article className="finding-row">
      <strong>{displayFinding.severity} · {displayFinding.ruleId} {displayFinding.ruleName}</strong>
      <p>{displayFinding.message}</p>
      {displayFinding.recommendation && <p className="hint">{displayFinding.recommendation}</p>}
      <span>{displayFinding.workflowPath ?? 'Project'}{displayFinding.activityDisplayName ? ` · ${displayFinding.activityDisplayName}` : ''}</span>
      {isAggregated && (
        <div className="aggregation-summary">
          <span>{finding.affectedActivityCount ?? finding.occurrenceCount ?? 0} {t('affectedActivities')}</span>
          {typeof finding.totalRelevantActivityCount === 'number' && <span>{finding.totalRelevantActivityCount} {t('relevantActivities')}</span>}
          {typeof finding.percentage === 'number' && <span>{finding.percentage.toFixed(1)}%</span>}
          {examples.length > 0 && (
            <ul>
              {examples.map((activity) => (
                <li key={activity.activityId ?? `${activity.activityName}-${activity.activityPath}`}>
                  {activity.activityName} · {activity.activityDisplayName}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
      {onFix && (
        <div className="row-actions">
          <button type="button" onClick={() => onFix(finding)} disabled={isFixLoading}>
            <Wrench size={16} />
            {isFixLoading ? t('generating') : t('fixSuggestion')}
          </button>
        </div>
      )}
    </article>
  );
}

function FixSuggestionPanel({
  result,
  applyResult,
  isApplyingFix = false,
  t,
  onApply,
  onGenerateAi,
}: {
  result: FixSuggestionResult;
  applyResult?: FixApplyResult | null;
  isApplyingFix?: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
  onApply?: (suggestion: FixSuggestion) => void;
  onGenerateAi?: () => void;
}) {
  const suggestion = result.suggestion;
  if (!suggestion) {
    return (
      <section className="fix-panel">
        <h2>{t('suggestedFix')}</h2>
        <p>{result.message ?? t('noFixAvailable')}</p>
        {onGenerateAi && (
          <>
            <p className="notice">{t('aiFixPrivacy')}</p>
            <button type="button" onClick={onGenerateAi}>{t('generateAiSuggestion')}</button>
          </>
        )}
      </section>
    );
  }

  return (
    <section className="fix-panel">
      <h2>{t('suggestedFix')}</h2>
      <div className="metric-grid">
        <Metric label={t('rule')} value={suggestion.ruleId} />
        <Metric label={t('fixType')} value={suggestion.fixType} />
        <Metric label={t('fixability')} value={suggestion.fixability ?? (suggestion.canAutoApply ? 'SafeAutomatic' : 'Previewable')} />
        <Metric label={t('risk')} value={suggestion.riskLevel} />
        <Metric label={t('confidence')} value={suggestion.confidence} />
        <Metric label={t('aiAssisted')} value={suggestion.requiresAi ? t('yes') : t('no')} />
        <Metric label={t('autoApply')} value={suggestion.canAutoApply ? t('available') : t('previewOnly')} />
      </div>
      {suggestion.confidence === 'Low' && <p className="notice">{t('reviewLowConfidenceFix')}</p>}
      {suggestion.riskLevel === 'High' && <p className="notice high-risk">{t('highRiskFix')}</p>}
      {suggestion.requiresAi && <p className="notice">{t('aiSuggestionManualReview')}</p>}
      <h3>{suggestion.title}</h3>
      <p>{suggestion.description}</p>
      <p><strong>{t('why')}</strong> {suggestion.explanation}</p>
      <div className="preview-grid">
        <div>
          <strong>{t('before')}</strong>
          <pre>{suggestion.patchPreview?.before ?? suggestion.beforePreview ?? t('noBeforePreview')}</pre>
        </div>
        <div>
          <strong>{t('after')}</strong>
          <pre>{suggestion.patchPreview?.after ?? suggestion.afterPreview ?? t('noAfterPreview')}</pre>
        </div>
      </div>
      {(suggestion.steps ?? []).length > 0 && (
        <section>
          <h3>{t('steps')}</h3>
          <ul>{suggestion.steps!.map((step) => <li key={step}>{step}</li>)}</ul>
        </section>
      )}
      {(suggestion.risks ?? []).length > 0 && (
        <section>
          <h3>{t('risks')}</h3>
          <ul>{suggestion.risks!.map((risk) => <li key={risk}>{risk}</li>)}</ul>
        </section>
      )}
      {(suggestion.userInputHints ?? []).length > 0 && (
        <section>
          <h3>{t('userInputNeeded')}</h3>
          <ul>{suggestion.userInputHints!.map((hint) => <li key={hint}>{hint}</li>)}</ul>
        </section>
      )}
      {(suggestion.validationNotes ?? []).length > 0 && (
        <section>
          <h3>{t('validationNotes')}</h3>
          <ul>{suggestion.validationNotes!.map((note) => <li key={note}>{note}</li>)}</ul>
        </section>
      )}
      <button type="button" onClick={() => copyFixInstructions(suggestion)}>{t('copyFixInstructions')}</button>
      {result.validation && !result.validation.isValid && (
        <p className="error-text">{result.message ?? t('fixSuggestionStale')}</p>
      )}
      {suggestion.canAutoApply && !suggestion.requiresAi ? (
        <button className="primary-action" type="button" onClick={() => onApply?.(suggestion)} disabled={isApplyingFix}>
          <Wrench size={16} />
          {isApplyingFix ? t('applying') : t('applyFix')}
        </button>
      ) : (
        <p className="notice">{t('manualChangeRequired')}</p>
      )}
      {applyResult && (
        <section className={applyResult.success ? 'apply-result success' : 'apply-result error'}>
          <h3>{applyResult.success ? t('fixAppliedSuccessfully') : t('fixWasNotApplied')}</h3>
          <p>{applyResult.message}</p>
          {applyResult.workflowPath && <p>Workflow: {applyResult.workflowPath}</p>}
          {applyResult.previousValue && <p>{t('before')}: {applyResult.previousValue}</p>}
          {applyResult.newValue && <p>{t('after')}: {applyResult.newValue}</p>}
          {applyResult.backupPath && <p>{t('backupCreated')}: {applyResult.backupPath}</p>}
          {applyResult.requiresReanalysis && <p>{t('projectFilesChangedReanalysis')}</p>}
        </section>
      )}
      {!suggestion.requiresAi && onGenerateAi && (
        <>
          <p className="notice">{t('aiFixPrivacy')}</p>
          <button type="button" onClick={onGenerateAi}>{t('generateAiSuggestion')}</button>
        </>
      )}
    </section>
  );
}

function copyFixInstructions(suggestion: FixSuggestion) {
  const sections = [
    `Fix: ${suggestion.title}`,
    `Rule: ${suggestion.ruleId}`,
    `Fixability: ${suggestion.fixability ?? (suggestion.canAutoApply ? 'SafeAutomatic' : 'Previewable')}`,
    `Confidence: ${suggestion.confidence}`,
    `Workflow: ${suggestion.workflowPath ?? 'Project'}`,
    suggestion.activityDisplayName ? `Activity: ${suggestion.activityDisplayName}` : null,
    '',
    'Why:',
    suggestion.explanation,
    '',
    'Current:',
    suggestion.patchPreview?.before ?? suggestion.beforePreview ?? suggestion.currentState ?? 'n/a',
    '',
    'Proposed:',
    suggestion.patchPreview?.after ?? suggestion.afterPreview ?? suggestion.proposedState ?? 'n/a',
    '',
    ...(suggestion.steps?.length ? ['Steps:', ...suggestion.steps.map((step) => `- ${step}`), ''] : []),
    ...(suggestion.risks?.length ? ['Risks:', ...suggestion.risks.map((risk) => `- ${risk}`), ''] : []),
  ].filter((line): line is string => line !== null);

  void navigator.clipboard?.writeText(sections.join('\n'));
}

function ApplyFixDialog({
  suggestion,
  isApplying,
  t,
  onCancel,
  onConfirm,
}: {
  suggestion: FixSuggestion;
  isApplying: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div className="modal-backdrop" role="presentation">
      <section className="confirmation-dialog" role="dialog" aria-modal="true" aria-labelledby="apply-fix-title">
        <h2 id="apply-fix-title">{t('applyThisChange')}</h2>
        <p>Workflow: {suggestion.workflowPath}</p>
        <p>{t('property')}: {suggestion.propertyName}</p>
        <div className="preview-grid">
          <div>
            <strong>{t('before')}</strong>
            <pre>{suggestion.currentValue ?? suggestion.beforePreview ?? t('unknown')}</pre>
          </div>
          <div>
            <strong>{t('after')}</strong>
            <pre>{suggestion.suggestedValue ?? suggestion.afterPreview ?? t('unknown')}</pre>
          </div>
        </div>
        <p className="notice">{t('backupWillBeCreated')}</p>
        <div className="dialog-actions">
          <button type="button" onClick={onCancel} disabled={isApplying}>{t('cancel')}</button>
          <button className="primary-action" type="button" onClick={onConfirm} disabled={isApplying}>
            {isApplying ? t('applying') : t('applyFix')}
          </button>
        </div>
      </section>
    </div>
  );
}

function ChangeHistory({
  backups,
  snapshots,
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
  selectedComparison?: AnalysisComparison | null;
  comparisonLoading: boolean;
  isLoading: boolean;
  undoResult?: UndoResult | null;
  t: (key: string, values?: Record<string, unknown>) => string;
  onRefresh: () => void;
  onCompare: (snapshot: AnalysisSnapshotSummary) => void;
  onUndo: (backup: BackupSummary) => void;
}) {
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
        {snapshots.length === 0 ? (
          <p className="empty-state">{t('noAnalysisHistory')}</p>
        ) : (
          <div className="history-list">
            <p className="hint">{t('allProjectsHistory')}</p>
            {snapshots.map((snapshot) => (
              <article className="history-entry" key={snapshot.snapshotId}>
                <div>
                  <strong>{formatTimestamp(snapshot.generatedAtUtc)}</strong>
                  <p>{snapshot.projectName ?? '-'} · {snapshot.projectPath ?? '-'}</p>
                  <p>{t('score')}: {snapshot.score} · {t('grade')} {snapshot.grade} · {snapshot.totalFindings} {t('findingsNav')}</p>
                  <p>{snapshot.workflowCount} {t('workflows')} · {snapshot.totalActivityCount} {t('activities')}</p>
                  {snapshot.scoreDelta !== null && snapshot.scoreDelta !== undefined && (
                    <span>{t('scoreChange')}: {formatSigned(snapshot.scoreDelta)} · {t('newFindings')}: {snapshot.newFindingCount ?? 0} · {t('resolvedFindings')}: {snapshot.resolvedFindingCount ?? 0}</span>
                  )}
                </div>
                <div className="history-actions">
                  {snapshot.previousSnapshotId ? (
                    <button type="button" onClick={() => onCompare(snapshot)} disabled={comparisonLoading}>
                      {comparisonLoading ? t('loading') : t('comparePrevious')}
                    </button>
                  ) : (
                    <span className="hint">{t('noPreviousSnapshot')}</span>
                  )}
                </div>
              </article>
            ))}
          </div>
        )}
      </section>
      {selectedComparison && (
        <section className="comparison-panel">
          <h3>{t('beforeAfterComparison')}</h3>
          <div className="metric-grid compact">
            <Metric label={t('scoreChange')} value={formatSigned(selectedComparison.scoreDelta)} />
            <Metric label={t('findingChange')} value={formatSigned(selectedComparison.totalFindingDelta)} />
            <Metric label={t('newFindings')} value={selectedComparison.newFindings.length} />
            <Metric label={t('resolvedFindings')} value={selectedComparison.resolvedFindings.length} />
            <Metric label={t('unchangedFindings')} value={selectedComparison.unchangedFindings.length} />
            <Metric label={t('changedFindings')} value={selectedComparison.changedFindings.length} />
          </div>
          <ComparisonFindingList title={t('newFindings')} findings={selectedComparison.newFindings} emptyLabel={t('none')} />
          <ComparisonFindingList title={t('resolvedFindings')} findings={selectedComparison.resolvedFindings} emptyLabel={t('none')} />
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
                    <td>{workflow.complexityScoreDelta === null || workflow.complexityScoreDelta === undefined ? '-' : formatSigned(workflow.complexityScoreDelta)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      )}
      {undoResult && (
        <section className={undoResult.success ? 'apply-result success' : 'apply-result error'}>
          <h3>{undoResult.success ? t('changeRestoredSuccessfully') : t('changeWasNotRestored')}</h3>
          <p>{undoResult.message}</p>
          {undoResult.workflowPath && <p>Workflow: {undoResult.workflowPath}</p>}
          {undoResult.safetyBackupId && <p>{t('safetyBackupCreated')}: {undoResult.safetyBackupId}</p>}
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
                <p>{backup.ruleId ?? t('unknownRule')} · {backup.propertyName ?? t('property')}</p>
                <p>{backup.previousValue ?? 'Before'} → {backup.newValue ?? 'After'}</p>
                <span>{t('applied')}: {formatTimestamp(backup.createdAtUtc)}</span>
              </div>
              <div className="history-actions">
                <span className={`history-status ${backup.status.toLowerCase()}`}>{backup.status}</span>
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

function ComparisonFindingList({ title, findings, emptyLabel }: { title: string; findings: AnalysisComparison['newFindings']; emptyLabel: string }) {
  return (
    <div className="comparison-findings">
      <h4>{title}</h4>
      {findings.length === 0 ? (
        <p className="empty-state">{emptyLabel}</p>
      ) : (
        <ul>
          {findings.slice(0, 10).map((item) => (
            <li key={`${item.state}-${item.finding.id}-${item.finding.contentHash ?? item.finding.message}`}>
              <strong>{item.finding.ruleId}</strong> {item.finding.workflowPath ?? ''} · {item.finding.message ?? item.finding.ruleName}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function formatSigned(value: number): string {
  return value > 0 ? `+${value}` : value.toString();
}

function UndoDialog({
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
      <section className="confirmation-dialog" role="dialog" aria-modal="true" aria-labelledby="undo-fix-title">
        <h2 id="undo-fix-title">{t('undoThisChange')}</h2>
        <p>Workflow: {backup.workflowPath}</p>
        <p>{t('current')}: {backup.newValue}</p>
        <p>{t('restore')}: {backup.previousValue}</p>
        <p className="notice">{t('backupWillBeCreated')}</p>
        <div className="dialog-actions">
          <button type="button" onClick={onCancel} disabled={isUndoing}>{t('cancel')}</button>
          <button className="primary-action" type="button" onClick={onConfirm} disabled={isUndoing}>
            {isUndoing ? t('undoing') : t('undoChange')}
          </button>
        </div>
      </section>
    </div>
  );
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

function WorkflowHealth({ workflows, onReview, isReviewing = false, t = (key, values) => translate('en', key, values) }: { workflows: ReturnType<typeof getWorkflowHealth>; onReview?: (workflowPath: string) => void; isReviewing?: boolean; t?: (key: string, values?: Record<string, unknown>) => string }) {
  return (
    <table>
      <thead>
        <tr>
          <th>Workflow</th>
          <th>{t('findingsNav')}</th>
          <th>{t('activities')}</th>
          <th>{t('complexity')}</th>
          {onReview && <th>{t('aiReview')}</th>}
        </tr>
      </thead>
      <tbody>
        {workflows.map((workflow) => (
          <tr key={workflow.relativePath}>
            <td>{workflow.relativePath}</td>
            <td>{workflow.findingCount}</td>
            <td>{workflow.activityCount}</td>
            <td>{localizeComplexityLevel(workflow.complexity?.complexityLevel, t)}</td>
            {onReview && <td><button type="button" onClick={() => onReview(workflow.relativePath)} disabled={isReviewing}>{t('reviewWorkflowWithAi')}</button></td>}
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function DependenciesView({ dependencyAnalysis, findings, locale, t }: { dependencyAnalysis: DependencySummary | null; findings: Finding[]; locale: Locale; t: (key: string, values?: Record<string, unknown>) => string }) {
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
    return (!query.trim() || text.includes(query.trim().toLowerCase()))
      && (category === 'All' || item.category === category)
      && (usage === 'All' || item.usageStatus === usage)
      && (risk === 'All' || item.riskLevel === risk);
  });
  const relatedFindings = selected
    ? findings.filter((finding) => finding.currentValue?.includes(selected.name) || finding.message?.includes(selected.name))
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
          <Metric label={t('thirdPartyDependencies')} value={dependencyAnalysis.thirdPartyDependencies} />
          <Metric label={t('possiblyUnused')} value={dependencyAnalysis.possiblyUnusedDependencies} />
          <Metric label={t('potentialConflicts')} value={dependencyAnalysis.potentialConflicts} />
          <Metric label={t('modernClassicMode')} value={dependencyAnalysis.modernClassicMode} />
        </div>
        <div className="workflow-tools">
          <input aria-label={t('searchPackages')} value={query} onChange={(event) => setQuery(event.target.value)} placeholder={t('searchPackages')} />
          <select aria-label={t('category')} value={category} onChange={(event) => setCategory(event.target.value)}>
            {categories.map((item) => <option key={item} value={item}>{item === 'All' ? t('all') : item}</option>)}
          </select>
          <select aria-label={t('usage')} value={usage} onChange={(event) => setUsage(event.target.value)}>
            {usages.map((item) => <option key={item} value={item}>{item === 'All' ? t('all') : item}</option>)}
          </select>
          <select aria-label={t('risk')} value={risk} onChange={(event) => setRisk(event.target.value)}>
            {risks.map((item) => <option key={item} value={item}>{item === 'All' ? t('all') : item}</option>)}
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
                <td><Package size={14} /> {item.name}</td>
                <td>{item.declaredVersion ?? '-'}</td>
                <td>{item.category}</td>
                <td>{item.usageStatus}</td>
                <td><span className={`status-badge ${item.riskLevel === 'Low' ? 'good' : item.riskLevel === 'Medium' ? 'review' : 'risk'}`}>{item.riskLevel}</span></td>
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
            <button type="button" onClick={() => setSelected(null)}>{t('close')}</button>
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
            {(selected.usedActivities ?? []).length === 0 ? <p className="empty-state">{t('noMappedActivities')}</p> : (
              <div className="chip-list">{selected.usedActivities!.map((activity) => <span className="workflow-chip" key={activity}>{activity}</span>)}</div>
            )}
          </details>
          <details open>
            <summary>{t('usedByWorkflows')}</summary>
            {(selected.usedByWorkflows ?? []).length === 0 ? <p className="empty-state">{t('noMappedWorkflows')}</p> : (
              <div className="chip-list">{selected.usedByWorkflows!.map((workflow) => <span className="workflow-chip" key={workflow}>{workflow}</span>)}</div>
            )}
          </details>
          <details open>
            <summary>{t('compatibilityNotes')}</summary>
            {(selected.findings ?? []).length === 0 && !selected.notes ? <p className="empty-state">{t('noDependencyRisks')}</p> : (
              <ul>{[...(selected.findings ?? []), selected.notes].filter(Boolean).map((note) => <li key={note}>{note}</li>)}</ul>
            )}
          </details>
          <details>
            <summary>{t('relatedFindings')}</summary>
            {relatedFindings.length === 0 ? <p className="empty-state">{t('noRelatedFindings')}</p> : relatedFindings.map((finding) => (
              <FindingRow key={`${finding.ruleId}-${finding.currentValue}`} finding={finding} locale={locale} t={t} />
            ))}
          </details>
        </aside>
      )}
    </div>
  );
}

function WorkflowsView({
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
  onReview,
  isReviewing,
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
  onReview: (workflowPath: string) => void;
  isReviewing: boolean;
  onProjectChanged: () => void;
}) {
  const invocationGraph = React.useMemo(() => buildInvocationGraph(allWorkflows), [allWorkflows]);
  const selectedDetail = selectedWorkflow ? buildWorkflowDetail(selectedWorkflow, allWorkflows, findings, invocationGraph, dependencyAnalysis) : null;
  const [conversionResult, setConversionResult] = React.useState<FlowchartConversionResult | null>(null);
  const [conversionError, setConversionError] = React.useState<string | null>(null);
  const [conversionLoading, setConversionLoading] = React.useState(false);
  const [conversionApplyResult, setConversionApplyResult] = React.useState<FlowchartConversionApplyResult | null>(null);
  const [conversionRollbackResult, setConversionRollbackResult] = React.useState<FlowchartConversionRollbackResult | null>(null);
  const [conversionApplying, setConversionApplying] = React.useState(false);
  const [conversionRollingBack, setConversionRollingBack] = React.useState(false);
  const [confirmConversionOpen, setConfirmConversionOpen] = React.useState(false);

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
      const result = await analyzeFlowchartConversion({
        projectPath,
        workflowPath: selectedWorkflow.relativePath,
      }) as FlowchartConversionResult;
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
      const result = await applyFlowchartConversion({
        projectPath,
        workflowPath: selectedWorkflow.relativePath,
        expectedWorkflowHash: conversionResult.workflowHash,
        confirmed: true,
        createBackup: true,
      }) as FlowchartConversionApplyResult;
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
      const result = await rollbackFlowchartConversion({
        projectPath,
        workflowPath: selectedWorkflow.relativePath,
        backupId: conversionApplyResult.backupId,
        expectedCurrentHash: conversionApplyResult.convertedHash,
        createSafetyBackup: true,
      }) as FlowchartConversionRollbackResult;
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
          <input aria-label="Workflow search" value={query} onChange={(event) => onQueryChange(event.target.value)} placeholder={t('searchWorkflows')} />
          <select aria-label="Workflow type filter" value={typeFilter} onChange={(event) => onTypeFilterChange(event.target.value)}>
            {categories.map((category) => <option key={category} value={category}>{category === 'All' ? t('all') : category}</option>)}
          </select>
          <select aria-label="Workflow sort" value={sort} onChange={(event) => onSortChange(event.target.value)}>
            <option value="findings">{t('byFindings')}</option>
            <option value="activities">{t('byActivity')}</option>
            <option value="name">{t('byName')}</option>
          </select>
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
              <th>{t('aiReview')}</th>
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
                  <td><span className={`status-badge ${complexityBadgeClass(workflow.complexity?.complexityLevel)}`}>{localizeComplexityLevel(workflow.complexity?.complexityLevel, t)}</span></td>
                  <td>{workflowScore}</td>
                  <td><span className={`status-badge ${workflowScore >= 85 ? 'good' : workflowScore >= 70 ? 'review' : 'risk'}`}>{workflowScore >= 85 ? t('good') : workflowScore >= 70 ? t('reviewNeeded') : t('risky')}</span></td>
                  <td><button type="button" aria-label="Review with AI" onClick={(event) => { event.stopPropagation(); onReview(workflow.relativePath); }} disabled={isReviewing}>{t('reviewWorkflowWithAi')}</button></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </section>
      {selectedWorkflow && selectedDetail && (
        <aside className="detail-drawer" aria-label="Workflow detail">
          <div className="section-header">
            <div>
              <span className="eyebrow">{t('workflowDetail')}</span>
              <h2>{getFileName(selectedWorkflow.relativePath)}</h2>
            </div>
            <button type="button" onClick={() => onSelectWorkflow(null)}>{t('backToWorkflows')}</button>
          </div>
          <div className="metric-grid compact">
            <Metric label={t('fileName')} value={getFileName(selectedWorkflow.relativePath)} />
            <Metric label={t('relativePath')} value={selectedWorkflow.relativePath} />
            <Metric label={t('activityCount')} value={selectedWorkflow.activityCount} />
            <Metric label={t('executableActivityCount')} value={selectedDetail.executableActivityCount} />
            <Metric label={t('findingCount')} value={selectedWorkflow.findingCount} />
            <Metric label={t('structureType')} value={selectedWorkflow.workflow.structureType ?? t('unknown')} />
            {selectedWorkflow.workflow.containsFlowchart && <Metric label={t('flowchartCount')} value={selectedWorkflow.workflow.flowchartCount ?? 1} />}
          </div>
          {selectedWorkflow.workflow.structureType === 'Flowchart' ? (
            <details open>
              <summary>{t('flowchartConversion')}</summary>
              <div className="conversion-actions">
                <button type="button" onClick={() => void handleAnalyzeConversion()} disabled={conversionLoading || !projectPath}>
                  <GitBranch size={16} />
                  {conversionLoading ? t('analyzing') : t('previewConversion')}
                </button>
                {conversionResult?.plan && (
                  <button type="button" onClick={() => copyText(formatConversionPlan(conversionResult, t))}>
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
                <Metric label={t('complexityLevel')} value={localizeComplexityLevel(selectedDetail.complexity.complexityLevel, t)} />
                <Metric label={t('complexityScore')} value={selectedDetail.complexity.complexityScore ?? 0} />
                <Metric label={t('executableActivityCount')} value={selectedDetail.complexity.executableActivities ?? selectedDetail.executableActivityCount} />
                <Metric label={t('containerActivityCount')} value={selectedDetail.complexity.containerActivities ?? 0} />
                <Metric label={t('maxNestingDepth')} value={selectedDetail.complexity.maxNestingDepth ?? 0} />
                <Metric label={t('decisionCount')} value={selectedDetail.complexity.decisionCount ?? 0} />
                <Metric label={t('loopCount')} value={selectedDetail.complexity.loopCount ?? 0} />
                <Metric label={t('tryCatchCount')} value={selectedDetail.complexity.tryCatchCount ?? 0} />
                <Metric label={t('invokeWorkflowCount')} value={selectedDetail.complexity.invokeWorkflowCount ?? 0} />
                <Metric label={t('arguments')} value={selectedDetail.complexity.argumentCount ?? selectedDetail.arguments.length} />
              </div>
            </details>
          )}
          <details open>
            <summary>{t('arguments')}</summary>
            {selectedDetail.arguments.length === 0 ? <p className="empty-state">{t('noArguments')}</p> : (
              <table className="compact-table">
                <thead><tr><th>{t('name')}</th><th>{t('direction')}</th><th>{t('type')}</th></tr></thead>
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
            {selectedDetail.invoked.length === 0 ? <p className="empty-state">{t('noInvokes')}</p> : (
              <div className="chip-list">
                {selectedDetail.invoked.map((invoke) => invoke.targetWorkflow ? (
                  <button key={`${invoke.sourceActivityId}-${invoke.rawPath}`} type="button" onClick={() => onSelectWorkflow(invoke.targetWorkflow!)}>
                    {invoke.normalizedPath}
                  </button>
                ) : (
                  <span className={`workflow-chip ${invoke.isDynamic ? 'dynamic' : 'missing'}`} key={`${invoke.sourceActivityId}-${invoke.rawPath}`}>
                    {invoke.isDynamic ? t('dynamicWorkflowReference') : invoke.normalizedPath}
                  </span>
                ))}
              </div>
            )}
          </details>
          <details>
            <summary>{t('dependenciesUsed')}</summary>
            {selectedDetail.dependenciesUsed.length === 0 ? <p className="empty-state">{t('noDependenciesUsed')}</p> : (
              <div className="chip-list">
                {selectedDetail.dependenciesUsed.map((dependency) => <span className="workflow-chip" key={dependency}>{dependency}</span>)}
              </div>
            )}
          </details>
          <details>
            <summary>{t('calledBy')}</summary>
            {selectedDetail.callers.length === 0 ? <p className="empty-state">{t('noCallers')}</p> : (
              <div className="chip-list">
                {selectedDetail.callers.map((caller) => (
                  <button key={caller.relativePath} type="button" onClick={() => onSelectWorkflow(caller)}>
                    {caller.relativePath}
                  </button>
                ))}
              </div>
            )}
          </details>
          <details>
            <summary>{t('activityTypes')}</summary>
            <div className="activity-type-grid">
              {selectedDetail.activityTypes.map((item) => <Metric key={item.name} label={item.name} value={item.count} />)}
            </div>
          </details>
          <details open>
            <summary>{t('activityTree')}</summary>
            {selectedDetail.activities.length === 0 ? <p className="empty-state">{t('noActivities')}</p> : <ActivityTree activities={selectedDetail.activities} />}
          </details>
          <details open>
            <summary>{t('workflowFindings')}</summary>
            {selectedDetail.findings.length === 0 ? <p className="empty-state">{t('noWorkflowFindings')}</p> : selectedDetail.findings.map((finding) => (
              <FindingRow key={`${finding.ruleId}-${finding.workflowPath}-${finding.activityId}-${finding.message}`} finding={finding} locale={locale} t={t} />
            ))}
          </details>
          <button className="primary-action" type="button" aria-label="Review with AI" onClick={() => onReview(selectedWorkflow.relativePath)} disabled={isReviewing}>
            {isReviewing ? t('analyzingWithAi') : t('reviewWorkflowWithAi')}
          </button>
        </aside>
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

function FlowchartConversionPanel({
  result,
  applyResult,
  rollbackResult,
  isApplying,
  isRollingBack,
  t,
  onApply,
  onRollback,
}: {
  result: FlowchartConversionResult;
  applyResult: FlowchartConversionApplyResult | null;
  rollbackResult: FlowchartConversionRollbackResult | null;
  isApplying: boolean;
  isRollingBack: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
  onApply: () => void;
  onRollback: () => void;
}) {
  const canApply = result.assessment?.conversionLevel === 'Safe';
  return (
    <div className="conversion-panel">
      <div className="metric-grid compact">
        <Metric label={t('currentStructure')} value={result.structureType} />
        <Metric label={t('convertibility')} value={localizeFlowchartLevel(result.assessment?.conversionLevel, t)} />
        <Metric label={t('confidence')} value={localizeFlowchartConfidence(result.assessment?.confidence, t)} />
        <Metric label={t('nodes')} value={result.graph?.nodes.length ?? 0} />
        <Metric label={t('decisions')} value={result.graph?.decisions.length ?? 0} />
        <Metric label={t('switches')} value={result.graph?.switches.length ?? 0} />
        <Metric label={t('cycles')} value={result.graph?.hasCycles ? t('yes') : t('no')} />
        <Metric label={t('unreachableNodes')} value={result.graph?.hasUnreachableNodes ? t('yes') : t('no')} />
      </div>

      <div className="before-after-grid">
        <section>
          <h3>{t('currentFlowchart')}</h3>
          <ul className="compact-list">
            {(result.graph?.nodes ?? []).slice(0, 12).map((node) => (
              <li key={node.id}>{node.id} · {node.type} · {node.displayName ?? node.activityName ?? '-'}</li>
            ))}
          </ul>
        </section>
        <section>
          <h3>{t('proposedSequence')}</h3>
          {result.plan?.previewTree ? <PreviewTree node={result.plan.previewTree} /> : <p className="empty-state">{t('noPreviewAvailable')}</p>}
        </section>
      </div>

      <details open>
        <summary>{t('risks')}</summary>
        {(result.assessment?.risks.length ?? 0) === 0 ? <p className="empty-state">{t('noRisks')}</p> : (
          <ul>{result.assessment?.risks.map((risk) => <li key={risk}>{risk}</li>)}</ul>
        )}
      </details>
      <details>
        <summary>{t('unsupportedPatterns')}</summary>
        {(result.assessment?.unsupportedPatterns.length ?? 0) === 0 ? <p className="empty-state">{t('noUnsupportedPatterns')}</p> : (
          <ul>{result.assessment?.unsupportedPatterns.map((item) => <li key={item}>{item}</li>)}</ul>
        )}
      </details>
      <details>
        <summary>{t('conversionMappings')}</summary>
        <table className="compact-table">
          <thead><tr><th>{t('sourceNode')}</th><th>{t('targetPath')}</th><th>{t('type')}</th></tr></thead>
          <tbody>
            {(result.plan?.mappings ?? []).map((mapping) => (
              <tr key={`${mapping.sourceNodeId}-${mapping.targetPath}`}>
                <td>{mapping.sourceNodeId}</td>
                <td>{mapping.targetPath}</td>
                <td>{mapping.transformationType}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
      <section className="conversion-apply-panel">
        <h3>{t('applyConversion')}</h3>
        <p className="empty-state">{canApply ? t('conversionApplyWarning') : t('conversionManualReviewOnly')}</p>
        <div className="conversion-actions">
          {canApply ? (
            <button type="button" onClick={onApply} disabled={isApplying || applyResult?.applied === true}>
              {isApplying ? t('applying') : t('applyConversion')}
            </button>
          ) : (
            <span className="status-badge review">{t('manualReviewRequired')}</span>
          )}
          {applyResult?.rollbackAvailable && applyResult.backupId && (
            <button type="button" onClick={onRollback} disabled={isRollingBack || rollbackResult?.restored === true}>
              {isRollingBack ? t('rollingBack') : t('rollback')}
            </button>
          )}
        </div>
        {applyResult && (
          <div className={`notice ${applyResult.success ? 'success' : 'error'}`}>
            <strong>{applyResult.success ? t('conversionAppliedSuccessfully') : t('conversionWasNotApplied')}</strong>
            <p>{applyResult.message}</p>
            {applyResult.backupId && <p>{t('backupCreated')}: {applyResult.backupId}</p>}
            {applyResult.requiresReanalysis && <p>{t('projectFilesChangedReanalysis')}</p>}
          </div>
        )}
        {rollbackResult && (
          <div className={`notice ${rollbackResult.success ? 'success' : 'error'}`}>
            <strong>{rollbackResult.success ? t('conversionRolledBackSuccessfully') : t('conversionRollbackFailed')}</strong>
            <p>{rollbackResult.message}</p>
            {rollbackResult.safetyBackupId && <p>{t('safetyBackupCreated')}: {rollbackResult.safetyBackupId}</p>}
            {rollbackResult.requiresReanalysis && <p>{t('projectFilesChangedReanalysis')}</p>}
          </div>
        )}
      </section>
    </div>
  );
}

function FlowchartConversionConfirmDialog({
  workflowPath,
  isApplying,
  t,
  onCancel,
  onConfirm,
}: {
  workflowPath: string;
  isApplying: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true" aria-label={t('applyConversion')}>
      <div className="modal-card">
        <h2>{t('applyConversion')}</h2>
        <p>{t('conversionApplyWarning')}</p>
        <div className="kv">
          <div>Workflow</div><div>{workflowPath}</div>
          <div>{t('currentStructure')}</div><div>Flowchart</div>
          <div>{t('proposedSequence')}</div><div>Sequence</div>
        </div>
        <p className="empty-state">{t('conversionBackupWillBeCreated')}</p>
        <div className="modal-actions">
          <button type="button" onClick={onCancel} disabled={isApplying}>{t('cancel')}</button>
          <button type="button" onClick={onConfirm} disabled={isApplying}>{isApplying ? t('applying') : t('confirmConversion')}</button>
        </div>
      </div>
    </div>
  );
}

function PreviewTree({ node }: { node: FlowchartPreviewNode }) {
  const label = [node.type, node.displayName, node.condition ? `[${node.condition}]` : null].filter(Boolean).join(' - ');
  return (
    <ul className="activity-tree">
      <li>
        <span>{label}</span>
        {(node.children?.length ?? 0) > 0 && (
          <ul>{node.children?.map((child, index) => <PreviewTree key={`${child.sourceNodeId ?? child.type}-${index}`} node={child} />)}</ul>
        )}
      </li>
    </ul>
  );
}

function formatConversionPlan(result: FlowchartConversionResult, t: (key: string, values?: Record<string, unknown>) => string): string {
  const lines = [
    `${t('flowchartConversion')}: ${result.workflowPath}`,
    `${t('currentStructure')}: ${result.structureType}`,
    `${t('convertibility')}: ${localizeFlowchartLevel(result.assessment?.conversionLevel, t)}`,
    `${t('confidence')}: ${localizeFlowchartConfidence(result.assessment?.confidence, t)}`,
    '',
    t('steps'),
    ...(result.plan?.steps ?? []).map((step) => `- ${step}`),
    '',
    t('risks'),
    ...((result.assessment?.risks.length ?? 0) === 0 ? [`- ${t('noRisks')}`] : result.assessment!.risks.map((risk) => `- ${risk}`)),
  ];
  return lines.join('\n');
}

function localizeFlowchartLevel(value: string | null | undefined, t: (key: string, values?: Record<string, unknown>) => string): string {
  if (!value) {
    return t('unknown');
  }

  return t(`flowchartLevel${value.replace(/\s/g, '')}`);
}

function localizeFlowchartConfidence(value: string | null | undefined, t: (key: string, values?: Record<string, unknown>) => string): string {
  if (!value) {
    return t('unknown');
  }

  return t(`flowchartConfidence${value.replace(/\s/g, '')}`);
}

function copyText(value: string) {
  if (navigator.clipboard) {
    void navigator.clipboard.writeText(value);
  }
}

function mergeStandaloneResults(current: StandaloneFlowchartAnalysisResult[], next: StandaloneFlowchartAnalysisResult[]): StandaloneFlowchartAnalysisResult[] {
  const byPath = new Map(current.map((item) => [item.filePath, item]));
  for (const item of next) {
    byPath.set(item.filePath, item);
  }

  return Array.from(byPath.values());
}

function defaultConvertedPath(sourcePath: string, suggestedFileName?: string): string {
  const normalized = sourcePath.replace(/\\/g, '/');
  const directory = normalized.includes('/') ? normalized.slice(0, normalized.lastIndexOf('/')) : '';
  const fileName = suggestedFileName || `${getFileName(sourcePath).replace(/\.xaml$/i, '')}_Sequence.xaml`;
  return directory ? `${directory}/${fileName}` : fileName;
}

function localizeStandaloneFlowchartStatus(status: string | null | undefined, t: (key: string, values?: Record<string, unknown>) => string): string {
  if (!status) {
    return t('unknown');
  }

  return t(`standaloneFlowchartStatus${status}`);
}

function flowchartStandaloneStatusClass(status: string | null | undefined): string {
  if (status === 'Ready' || status === 'Converted' || status === 'AlreadySequence') {
    return 'good';
  }

  if (status === 'RequiresReview' || status === 'Complex' || status === 'Unsupported') {
    return 'review';
  }

  return 'risk';
}

function buildInvocationGraph(workflows: ReturnType<typeof getWorkflowHealth>): Map<string, WorkflowInvocation[]> {
  const workflowByPath = new Map(workflows.map((workflow) => [normalizeWorkflowPath(workflow.relativePath), workflow]));
  const graph = new Map<string, WorkflowInvocation[]>();

  for (const workflow of workflows) {
    const invokes = (workflow.workflow.activities ?? [])
      .filter((activity) => normalizeActivityName(activity.name) === 'invokeworkflowfile')
      .map((activity) => {
        const rawPath = getActivityValue(activity, ['WorkflowFileName', 'WorkflowFile', 'FileName', 'Path']) ?? '';
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
  const workflowFindings = findings.filter((finding) => normalizeWorkflowPath(finding.workflowPath ?? '') === selectedPath);
  const invoked = invocationGraph.get(selectedPath) ?? [];
  const callers = allWorkflows.filter((workflow) =>
    (invocationGraph.get(normalizeWorkflowPath(workflow.relativePath)) ?? [])
      .some((invoke) => invoke.targetWorkflow && normalizeWorkflowPath(invoke.targetWorkflow.relativePath) === selectedPath),
  );
  const executableActivities = activities.filter(isExecutableActivity);
  const dependenciesUsed = (dependencyAnalysis?.packages ?? [])
    .filter((dependency) => (dependency.usedByWorkflows ?? []).some((workflow) => normalizeWorkflowPath(workflow) === selectedPath))
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
    activityTypes: Array.from(executableActivities.reduce((map, activity) => {
      const name = activity.name || 'Activity';
      map.set(name, (map.get(name) ?? 0) + 1);
      return map;
    }, new Map<string, number>()))
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

function ActivityTree({ activities }: { activities: Activity[] }) {
  const childrenByParent = new Map<string, Activity[]>();
  const ids = new Set(activities.map((activity) => activity.activityId));
  for (const activity of activities) {
    const parentKey = activity.parentActivityId && ids.has(activity.parentActivityId) ? activity.parentActivityId : '__root__';
    childrenByParent.set(parentKey, [...(childrenByParent.get(parentKey) ?? []), activity]);
  }

  const renderNode = (activity: Activity): React.ReactNode => {
    const children = childrenByParent.get(activity.activityId) ?? [];
    const label = activity.displayName && activity.displayName !== activity.name
      ? `${activity.name} · ${activity.displayName}`
      : activity.name;

    return (
      <li key={activity.activityId}>
        {children.length > 0 ? (
          <details open={(activity.depth ?? 0) < 1}>
            <summary>{label}</summary>
            <ActivityProperties activity={activity} />
            <ul>{children.map(renderNode)}</ul>
          </details>
        ) : (
          <div>
            <span>{label}</span>
            <ActivityProperties activity={activity} />
          </div>
        )}
      </li>
    );
  };

  return <ul className="activity-tree">{(childrenByParent.get('__root__') ?? activities.filter((activity) => (activity.depth ?? 0) === 0)).map(renderNode)}</ul>;
}

function ActivityProperties({ activity }: { activity: Activity }) {
  const properties = Object.entries(activity.properties ?? {}).filter(([, value]) => value !== null && value !== undefined && value !== '');
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
      if (names.some((name) => key.localeCompare(name, undefined, { sensitivity: 'accent' }) === 0) && value) {
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

  return /config\s*\(|path\.combine|string\.format|\+|\{|\}|\(|\)|\bin_|\bout_|\bvar_/i.test(trimmed) && !/^["'][^"']+\.xaml["']$/i.test(trimmed);
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

function AiReviewPanel({ result, isLoading, onAsk, t }: { result: AiReviewResult | null; isLoading: boolean; onAsk: (question: string) => void; t: (key: string, values?: Record<string, unknown>) => string }) {
  const quickActions = [
    t('aiQuickRiskSummary'),
    t('aiQuickRefactorWorkflows'),
    t('aiQuickSelectors'),
    t('aiQuickExceptionHandling'),
    t('aiQuickMaintainability'),
    t('aiQuickTopFindings'),
  ];

  return (
    <div className="ai-panel">
      <section className="list-panel">
        <span className="eyebrow">{t('aiReview')}</span>
        <h2>{t('aiPanelTitle')}</h2>
        <div className="suggestions">
          {quickActions.map((action) => (
            <button key={action} type="button" onClick={() => onAsk(action)}>
              {action}
            </button>
          ))}
        </div>
      </section>
      <p className="notice">{t('aiReviewInterpretationNotice')}</p>
      <p className="notice">{t('aiReviewPrivacyNotice')}</p>
      {isLoading && <p>{t('analyzingWithAi')}</p>}
      {!isLoading && !result && <p>{t('aiReviewEmptyState')}</p>}
      {result && (
        <>
          <div className="metric-grid">
            <Metric label={t('riskLevel')} value={result.riskLevel} />
            <Metric label={t('scope')} value={result.reviewedWorkflowPath ?? result.reviewedScope ?? 'Project'} />
            <Metric label={t('model')} value={result.model ?? 'Configured provider'} />
            <Metric label={t('confidence')} value={typeof result.confidence === 'number' ? result.confidence.toFixed(2) : 'n/a'} />
          </div>
          <section>
            <h2>{t('aiReviewSummary')}</h2>
            <p>{result.summary}</p>
          </section>
          <AiList title={t('strengths')} items={result.strengths ?? []} t={t} />
          <section>
            <h2>{t('issues')}</h2>
            {(result.issues ?? []).length === 0 ? <p>{t('noAiIssues')}</p> : result.issues!.map((issue) => (
              <article className="finding-row" key={`${issue.title}-${issue.workflowPath}`}>
                <strong>{issue.severity} · {issue.title}</strong>
                <p>{issue.description}</p>
                <span>{t('evidenceLabel')}: {issue.evidence}</span>
                <p>{issue.recommendation}</p>
                {(issue.relatedRuleIds ?? []).length > 0 && <span>{t('relatedDeterministicRules')}: {issue.relatedRuleIds!.join(', ')}</span>}
              </article>
            ))}
          </section>
          <AiList title={t('recommendations')} items={result.recommendations ?? []} t={t} />
          <AiList title={t('architectureObservations')} items={result.architectureObservations ?? []} t={t} />
        </>
      )}
    </div>
  );
}

function AiList({ title, items, t }: { title: string; items: string[]; t: (key: string, values?: Record<string, unknown>) => string }) {
  return (
    <section>
      <h2>{title}</h2>
      {items.length === 0 ? <p>{t('noItemsReturned')}</p> : <ul>{items.map((item) => <li key={item}>{item}</li>)}</ul>}
    </section>
  );
}

function AskProjectPanel({
  question,
  setQuestion,
  history,
  isLoading,
  t,
  onAsk,
}: {
  question: string;
  setQuestion: (value: string) => void;
  history: Array<{ question: string; answer: ProjectAnswer }>;
  isLoading: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
  onAsk: (question?: string) => void;
}) {
  const suggestedQuestions = [
    t('suggestedQueueQuestion'),
    t('suggestedDelayQuestion'),
    t('suggestedMostFindingsQuestion'),
    t('suggestedProcessQuestion'),
    t('suggestedInvocationQuestion'),
    t('suggestedExceptionQuestion'),
  ];

  return (
    <div className="ask-panel">
      <p className="notice">{t('askNotice')}</p>
      <section className="ask-box" aria-label={t('askLabel')}>
        <label htmlFor="projectQuestion">{t('askLabel')}</label>
        <textarea
          id="projectQuestion"
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          maxLength={2000}
          placeholder={t('askPlaceholder')}
        />
        <button type="button" onClick={() => onAsk()} disabled={isLoading || !question.trim()}>
          <Send size={18} />
          {isLoading ? t('asking') : t('ask')}
        </button>
      </section>

      {history.length === 0 && (
        <section>
          <h2>{t('suggestedQuestions')}</h2>
          <div className="suggestions">
            {suggestedQuestions.map((suggestion) => (
              <button key={suggestion} type="button" onClick={() => onAsk(suggestion)} disabled={isLoading}>
                {suggestion}
              </button>
            ))}
          </div>
        </section>
      )}

      <section className="answer-list">
        {history.map((item) => (
          <ProjectAnswerCard key={`${item.question}-${item.answer.generatedAtUtc ?? item.answer.answer}`} question={item.question} answer={item.answer} t={t} />
        ))}
      </section>
    </div>
  );
}

function ProjectAnswerCard({ question, answer, t }: { question: string; answer: ProjectAnswer; t: (key: string, values?: Record<string, unknown>) => string }) {
  return (
    <article className="answer-card">
      <span className="question-label">{question}</span>
      <h2>{t('answer')}</h2>
      <p>{answer.answer}</p>
      <div className="answer-meta">
        <span>{t('questionConfidence')}: {answer.confidence}</span>
        <span>{t('aiUsed')}: {answer.usedAi ? t('yes') : t('no')}</span>
        <span>{answer.usedAi ? t('aiAnswerBased') : t('answeredLocal')}</span>
      </div>
      {(answer.relatedWorkflows ?? []).length > 0 && <p><strong>{t('relatedWorkflows')}:</strong> {answer.relatedWorkflows!.join(', ')}</p>}
      {(answer.relatedRuleIds ?? []).length > 0 && <p><strong>{t('relatedRules')}:</strong> {answer.relatedRuleIds!.join(', ')}</p>}
      {answer.reasoningSummary && <p><strong>{t('reasoningSummary')}:</strong> {answer.reasoningSummary}</p>}
      {answer.errorMessage && <p className="error-text">{answer.errorMessage}</p>}
      <details>
        <summary>{t('evidence')} ({answer.evidence?.length ?? 0})</summary>
        <div className="evidence-list">
          {(answer.evidence ?? []).map((evidence, index) => (
            <div className="evidence-row" key={`${evidence.type}-${evidence.workflowPath}-${evidence.activityName}-${evidence.ruleId}-${index}`}>
              <strong>{evidence.type}</strong>
              {evidence.workflowPath && <span>Workflow: {evidence.workflowPath}</span>}
              {(evidence.activityDisplayName || evidence.activityName) && <span>{t('activity')}: {evidence.activityDisplayName ?? evidence.activityName}</span>}
              {evidence.ruleId && <span>{t('rule')}: {evidence.ruleId}</span>}
              {evidence.propertyName && <span>{t('property')}: {evidence.propertyName}</span>}
              {evidence.value && <span>{t('value')}: {evidence.value}</span>}
              {evidence.description && <p>{evidence.description}</p>}
            </div>
          ))}
        </div>
      </details>
    </article>
  );
}

function SettingsView({
  section,
  language,
  t,
  onSectionChange,
  onLanguageChange,
}: {
  section: string;
  language: 'tr' | 'en';
  t: (key: string, values?: Record<string, unknown>) => string;
  onSectionChange: (section: string) => void;
  onLanguageChange: (language: 'tr' | 'en') => void;
}) {
  const sections = ['profileSettings', 'appearance', 'languageSettings', 'aiPreferences', 'reviewRules', 'notifications', 'about'];

  return (
    <section className="settings-layout" aria-label={t('settings')}>
      <aside className="settings-nav">
        {sections.map((item) => (
          <button key={item} type="button" className={section === item ? 'active' : ''} onClick={() => onSectionChange(item)}>
            {t(item)}
          </button>
        ))}
      </aside>
      <div className="settings-panel">
        <div className="section-header">
          <div>
            <span className="eyebrow">{t('settings')}</span>
            <h2>{t(section)}</h2>
          </div>
          <Settings size={20} />
        </div>
        {section === 'profileSettings' && (
          <div className="profile-settings">
            <span className="avatar large">MD</span>
            <div>
              <h3>Mina Dilek</h3>
              <p>RPA Developer</p>
              <p>mina@example.local</p>
            </div>
          </div>
        )}
        {section === 'appearance' && (
          <div className="option-grid">
            {['lightTheme', 'darkTheme', 'systemTheme'].map((mode) => <button key={mode} type="button">{t(mode)}</button>)}
          </div>
        )}
        {section === 'languageSettings' && (
          <div className="option-grid">
            <button className={language === 'tr' ? 'active' : ''} type="button" onClick={() => onLanguageChange('tr')}>Türkçe</button>
            <button className={language === 'en' ? 'active' : ''} type="button" onClick={() => onLanguageChange('en')}>English</button>
          </div>
        )}
        {section === 'aiPreferences' && (
          <div className="settings-checks">
            {['aiReviewEnabled', 'autoReviewSummary', 'suggestedFixes', 'askProjectEnabled'].map((label) => (
              <label key={label}><input type="checkbox" defaultChecked /> {t(label)}</label>
            ))}
          </div>
        )}
        {section === 'reviewRules' && (
          <div className="settings-checks two-column">
            {['selectorQuality', 'exceptionHandling', 'logging', 'namingConvention', 'hardcodedValues', 'performance', 'maintainability', 'security', 'reframeworkBestPractices'].map((label) => (
              <label key={label}><input type="checkbox" defaultChecked /> {t(label)}</label>
            ))}
          </div>
        )}
        {(section === 'notifications' || section === 'about') && <p className="notice">{t('settingsNotice')}</p>}
      </div>
    </section>
  );
}

function ReportCreateDialog({ t, onCancel, onExport }: { t: (key: string, values?: Record<string, unknown>) => string; onCancel: () => void; onExport: (format: ReportFormat) => void }) {
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
      <section className="confirmation-dialog" role="dialog" aria-modal="true" aria-labelledby="report-create-title">
        <h2 id="report-create-title">{t('reportDialogTitle')}</h2>
        <div className="settings-checks two-column">
          {reportSections.map((label) => (
            <label key={label}><input type="checkbox" defaultChecked /> {label}</label>
          ))}
        </div>
        <p className="notice">{t('reportNotice')}</p>
        <div className="dialog-actions">
          <button type="button" onClick={onCancel}>{t('cancel')}</button>
          <button type="button" onClick={() => onExport('json')}>JSON</button>
          <button type="button" onClick={() => onExport('pdf')}>PDF</button>
          <button className="primary-action" type="button" onClick={() => onExport('html')}>{t('createHtmlReport')}</button>
        </div>
      </section>
    </div>
  );
}

function buildReviewSummary(analysis: AnalysisResponse, t: (key: string, values?: Record<string, unknown>) => string): string {
  const findings = analysis.analysis?.findings ?? [];
  if (findings.length === 0) {
    return t('noIssuesSummary');
  }

  const topCategories = Array.from(new Set(findings.map((finding) => finding.category).filter(Boolean))).slice(0, 3);
  return t('reviewSummaryText', { categories: topCategories.length ? topCategories.join(', ') : undefined });
}

function getWorkflowType(path: string): string {
  const normalized = path.replaceAll('\\', '/').toLowerCase();
  if (normalized.endsWith('/main.xaml') || normalized === 'main.xaml') {
    return 'Main';
  }

  if (normalized.includes('/framework/')) {
    return 'Framework';
  }

  if (normalized.includes('/business/')) {
    return 'Business';
  }

  if (normalized.includes('/utility/') || normalized.includes('/utilities/') || normalized.includes('/lib/')) {
    return 'Utility';
  }

  return 'Business';
}

function formatWorkflowStructure(workflow: { structureType?: string; containsFlowchart?: boolean; flowchartCount?: number }, t: (key: string, values?: Record<string, unknown>) => string): string {
  const structure = workflow.structureType ?? t('unknown');
  if (!workflow.containsFlowchart || structure === 'Flowchart') {
    return structure;
  }

  const count = workflow.flowchartCount && workflow.flowchartCount > 1 ? ` (${workflow.flowchartCount})` : '';
  return `${structure} + ${t('containsFlowchart')}${count}`;
}

function estimateWorkflowScore(workflow: ReturnType<typeof getWorkflowHealth>[number]): number {
  const findingPenalty = workflow.findingCount * 7;
  const sizePenalty = workflow.activityCount > 100 ? 8 : 0;
  return Math.max(40, Math.min(100, 100 - findingPenalty - sizePenalty));
}

function localizeComplexityLevel(level: string | null | undefined, t: (key: string, values?: Record<string, unknown>) => string): string {
  if (!level) {
    return t('unknown');
  }

  return level === 'VeryHigh' || level === 'Very High'
    ? t('veryHigh')
    : t(level.charAt(0).toLowerCase() + level.slice(1));
}

function complexityBadgeClass(level: string | null | undefined): string {
  return level === 'High' || level === 'VeryHigh' || level === 'Very High'
    ? 'risk'
    : level === 'Medium'
      ? 'review'
      : 'good';
}

function createDefaultCustomRule(): CustomRuleDefinition {
  return {
    id: `CUSTOM-${Date.now().toString().slice(-6)}`,
    name: 'Large Nested Workflow',
    description: 'Workflow matches company-defined complexity criteria.',
    recommendation: 'Review the workflow and split it when appropriate.',
    category: 'Maintainability',
    severity: 'Warning',
    scope: 'Workflow',
    enabled: true,
    weight: 2,
    maxPenalty: 10,
    matchMode: 'All',
    conditions: [
      { field: 'Workflow.ExecutableActivityCount', operator: 'GreaterThanOrEqual', value: '100', caseSensitive: false },
      { field: 'Workflow.MaxNestingDepth', operator: 'GreaterThanOrEqual', value: '8', caseSensitive: false },
    ],
  };
}

function createDefaultRuleProfile(rules: RuleCatalogItem[]): RuleProfile {
  return {
    id: `custom-profile-${Date.now().toString().slice(-6)}`,
    name: 'Company Standard',
    description: 'Local custom profile for company-specific rule configuration.',
    rules: rules.map((rule) => ({
      ruleId: rule.id,
      enabled: rule.enabledByDefault ?? true,
      severityOverride: null,
      weight: rule.defaultWeight ?? 1,
      maxPenalty: rule.defaultMaxPenalty ?? 10,
      description: rule.name,
    })),
  };
}

function ruleCategories(): string[] {
  return ['Reliability', 'Maintainability', 'Performance', 'Security', 'ExceptionHandling', 'Naming', 'Logging', 'UiAutomation', 'Architecture', 'Configuration', 'Orchestrator'];
}

function ruleSeverities(): string[] {
  return ['Info', 'Suggestion', 'Warning', 'Error', 'Critical'];
}

function ruleScopes(): string[] {
  return ['Activity', 'Workflow', 'Project'];
}

function conditionFields(): string[] {
  return [
    'Activity.Name',
    'Activity.DisplayName',
    'Activity.Property',
    'Workflow.Name',
    'Workflow.Path',
    'Workflow.ActivityCount',
    'Workflow.ExecutableActivityCount',
    'Workflow.MaxNestingDepth',
    'Project.Compatibility',
    'Project.IsReFramework',
    'Dependency.Name',
    'Dependency.Version',
    'Dependency.Category',
    'Dependency.UsageStatus',
    'Dependency.RiskLevel',
  ];
}

function conditionOperators(): string[] {
  return ['Equals', 'NotEquals', 'Contains', 'StartsWith', 'EndsWith', 'GreaterThan', 'GreaterThanOrEqual', 'LessThan', 'LessThanOrEqual', 'Exists', 'NotExists'];
}

function updateCondition(
  setDraft: React.Dispatch<React.SetStateAction<CustomRuleDefinition>>,
  index: number,
  patch: Partial<CustomRuleDefinition['conditions'][number]>,
): void {
  setDraft((rule) => ({
    ...rule,
    conditions: rule.conditions.map((condition, currentIndex) => currentIndex === index ? { ...condition, ...patch } : condition),
  }));
}

function removeCondition(setDraft: React.Dispatch<React.SetStateAction<CustomRuleDefinition>>, index: number): void {
  setDraft((rule) => ({
    ...rule,
    conditions: rule.conditions.filter((_, currentIndex) => currentIndex !== index),
  }));
}

function unique(values: string[]): string[] {
  return Array.from(new Set(values.filter(Boolean)));
}

function downloadJson(fileName: string, value: unknown): void {
  const blob = new Blob([JSON.stringify(value, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

function Metric({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="metric-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function safeGetStoredLocale(): string | null {
  try {
    return typeof window.localStorage?.getItem === 'function'
      ? window.localStorage.getItem('rpadevassistant.locale')
      : null;
  } catch {
    return null;
  }
}

function safeStoreLocale(locale: Locale): void {
  try {
    if (typeof window.localStorage?.setItem === 'function') {
      window.localStorage.setItem('rpadevassistant.locale', locale);
    }
  } catch {
    // Persistence is optional in test and restricted browser contexts.
  }
}

const rootElement = document.getElementById('root');
if (rootElement) {
  ReactDOM.createRoot(rootElement).render(<App />);
}

import React from 'react';
import ReactDOM from 'react-dom/client';
import {
  AlertTriangle,
  Bell,
  Bot,
  ChevronDown,
  CircleHelp,
  ClipboardCheck,
  Download,
  FileJson,
  FileSearch,
  FileSpreadsheet,
  FolderKanban,
  FolderOpen,
  History,
  Home,
  Play,
  RefreshCw,
  Settings,
  ShieldCheck,
  Workflow,
} from 'lucide-react';
import {
  analyzeProject,
  applyFix,
  askProject,
  checkHealth,
  compareAnalysisSnapshots,
  getBackendBaseUrl,
  getFixSuggestion,
  getRuleProfiles,
  getRules,
  listAnalysisHistory,
  listBackups,
  setApiLocale,
  undoFix,
  validateProject,
} from './services/apiClient';
import { isTauriDesktop, selectProjectFolder } from './services/projectFolderService';
import { exportReport, type ReportFormat } from './services/reportExportService';
import {
  getTopIssues,
  getWorkflowHealth,
  type AnalysisComparison,
  type AnalysisHistoryList,
  type AnalysisResponse,
  type AnalysisSnapshotSummary,
  type BackupListResult,
  type BackupSummary,
  type Finding,
  type FixApplyResult,
  type FixSuggestion,
  type FixSuggestionResult,
  type ProjectAnswer,
  type RuleCatalogItem,
  type RuleProfile,
  type UndoResult,
} from './services/reportViewModel';
import { translate, type Locale } from './localization';
import './styles.css';

import { Overview, DashboardEmpty } from './components/Overview';
import { Findings, ApplyFixDialog } from './components/FindingsView';
import { ChangeHistory, UndoDialog, findPreviousSnapshotId } from './components/HistoryView';
import { WorkflowsView } from './components/WorkflowsView';
import { DependenciesView } from './components/DependenciesView';
import { ConfigAnalysisView } from './components/ConfigAnalysisView';
import { FlowchartConverterView } from './components/FlowchartConverterView';
import { RulesView } from './components/RulesView';
import { AskProjectPanel } from './components/AskProjectPanel';
import { SettingsView } from './components/SettingsView';
import { ReportView, ReportCreateDialog } from './components/ReportView';
import { ProcessPddAnalysisView } from './components/ProcessPddAnalysisView';
import {
  type ActiveTab,
  type HealthState,
  getWorkflowType,
  normalizeCategoryValue,
  normalizeWorkflowPath,
  readProjectProfileId,
  safeGetStoredLocale,
  safeStoreLocale,
  saveProjectProfileId,
} from './components/uiUtils';

export function App() {
  const [projectPath, setProjectPath] = React.useState('');
  const [selectedFolder, setSelectedFolder] = React.useState<string | null>(null);
  const [healthState, setHealthState] = React.useState<HealthState>('checking');
  const [statusMessage, setStatusMessage] = React.useState('Checking backend...');
  const [analysis, setAnalysis] = React.useState<AnalysisResponse | null>(null);
  const [isAnalyzing, setIsAnalyzing] = React.useState(false);
  const [exportingFormat, setExportingFormat] = React.useState<ReportFormat | null>(null);
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
  const [findingRuleFilter, setFindingRuleFilter] = React.useState('All');
  const [findingQuery, setFindingQuery] = React.useState('');
  const [workflowQuery, setWorkflowQuery] = React.useState('');
  const [workflowTypeFilter, setWorkflowTypeFilter] = React.useState('All');
  const [workflowSort, setWorkflowSort] = React.useState('findings');
  const [selectedWorkflow, setSelectedWorkflow] = React.useState<ReturnType<typeof getWorkflowHealth>[number] | null>(null);
  const [settingsSection, setSettingsSection] = React.useState('profileSettings');
  const [rules, setRules] = React.useState<RuleCatalogItem[]>([]);
  const [rulesLoading, setRulesLoading] = React.useState(false);
  const [selectedRule, setSelectedRule] = React.useState<RuleCatalogItem | null>(null);
  const [ruleStatus, setRuleStatus] = React.useState('');
  const [ruleNotification, setRuleNotification] = React.useState<{ id: number; message: string } | null>(null);
  const [profiles, setProfiles] = React.useState<RuleProfile[]>([]);

  const desktop = isTauriDesktop();
  const t = React.useCallback((key: string, values?: Record<string, unknown>) => translate(language, key, values), [language]);
  const findings = React.useMemo(() => analysis?.analysis?.findings ?? [], [analysis]);
  const topIssues = React.useMemo(() => getTopIssues(findings), [findings]);
  const workflowHealth = React.useMemo(() => analysis ? getWorkflowHealth(analysis) : [], [analysis]);
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
  const findingCategories = React.useMemo(() => ['All', ...Array.from(new Set(findings.map((finding) => normalizeCategoryValue(finding.category)).filter(Boolean))) as string[]], [findings]);
  const findingRules = React.useMemo(() => ['All', ...Array.from(new Set(findings.map((finding) => finding.ruleId).filter(Boolean))).sort()], [findings]);
  const filteredFindings = React.useMemo(() => findings.filter((finding) => {
    const query = findingQuery.trim().toLowerCase();
    const severityMatches = findingSeverityFilter === 'All' || finding.severity === findingSeverityFilter;
    const categoryMatches = findingCategoryFilter === 'All' || normalizeCategoryValue(finding.category) === findingCategoryFilter;
    const ruleMatches = findingRuleFilter === 'All' || finding.ruleId === findingRuleFilter;
    const queryMatches = !query || [
      finding.ruleId,
      finding.ruleName,
      finding.message,
      finding.workflowPath,
      finding.activityName,
      finding.activityDisplayName,
      finding.recommendation,
    ].some((value) => value?.toLowerCase().includes(query));
    return severityMatches && categoryMatches && ruleMatches && queryMatches;
  }), [findings, findingCategoryFilter, findingQuery, findingRuleFilter, findingSeverityFilter]);

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

  React.useEffect(() => {
    if (activeTab === 'rules' && (!rules.length || !profiles.length) && healthState === 'ready') {
      void refreshRules();
      void refreshProfiles();
    }
  }, [activeTab, healthState, profiles.length, rules.length]);

  React.useEffect(() => {
    const savedProfileId = readProjectProfileId(projectPath);
    if (savedProfileId && savedProfileId !== selectedProfileId) {
      setSelectedProfileId(savedProfileId);
    }
  }, [projectPath]);

  React.useEffect(() => {
    if (!projectPath.trim()) {
      return;
    }

    saveProjectProfileId(projectPath, selectedProfileId);
  }, [projectPath, selectedProfileId]);

  async function refreshRules() {
    setRulesLoading(true);
    try {
      const result = (await getRules()) as RuleCatalogItem[];
      setRules(result);
      setSelectedRule((current) => current ? result.find((rule) => rule.id === current.id) ?? current : result[0] ?? null);
      setRuleStatus('');
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
      setRuleStatus('');
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
    const shouldStayOnConfigAnalysis = activeTab === 'config';
    try {
      const result = (await analyzeProject(projectPath.trim(), selectedProfileId)) as AnalysisResponse;
      setAnalysis(result);
      setAnalysisOutOfDate(false);
      setQuestionHistory([]);
      setFixResult(null);
      setApplyResult(null);
      await refreshBackups(projectPath.trim());
      await refreshAnalysisHistory();
      setSelectedComparison(result.comparisonWithPrevious ?? null);
      setSelectedFixFinding(null);
      setActiveTab(shouldStayOnConfigAnalysis ? 'config' : 'report');
      setStatusMessage(result.projectName ? t('analysisCompleted') : t('notUipathProject'));
    } catch {
      setStatusMessage(t('analysisFailed'));
    } finally {
      setIsAnalyzing(false);
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
    const previousSnapshotId = snapshot.previousSnapshotId ?? findPreviousSnapshotId(snapshot, analysisSnapshots);
    if (!comparisonProjectPath || !previousSnapshotId) {
      setStatusMessage(t('noPreviousSnapshot'));
      return;
    }

    setComparisonLoading(true);
    try {
      const result = await compareAnalysisSnapshots({
        projectPath: comparisonProjectPath,
        baselineSnapshotId: previousSnapshotId,
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

  const navigationItems: Array<{ label: string; tab?: ActiveTab; icon: React.ComponentType<{ size?: number }>; showActive?: boolean }> = [
    { label: t('home'), tab: 'overview', icon: Home },
    { label: t('projects'), tab: 'overview', icon: FolderKanban, showActive: false },
    { label: t('codeReview'), tab: 'workflows', icon: ClipboardCheck },
    { label: t('findingsNav'), tab: 'findings', icon: AlertTriangle },
    { label: t('rules'), tab: 'rules', icon: ShieldCheck },
    { label: t('configAnalysis'), tab: 'config', icon: FileSpreadsheet },
    { label: t('flowchartConverter'), tab: 'flowchartConverter', icon: Workflow },
    { label: t('processPddNav'), tab: 'processPdd', icon: FileSearch },
    { label: t('reports'), tab: 'report', icon: FileJson },
    { label: t('reviewHistory'), tab: 'history', icon: History },
  ];

  return (
    <main className="app-shell">
      <aside className="sidebar" aria-label={t('primaryNavigation')}>
        <div className="brand-lockup">
          <span className="brand-mark"><Bot size={18} /></span>
          <strong>RPA Dev Assistant</strong>
        </div>
        <nav className="sidebar-nav">
          {navigationItems.map((item) => {
            const NavigationIcon = item.icon;
            return (
              <button
                key={item.label}
                type="button"
                aria-label={`Sidebar ${item.label}`}
                className={item.showActive !== false && item.tab === activeTab ? 'active' : ''}
                onClick={() => item.tab && setActiveTab(item.tab)}
              >
                <NavigationIcon size={16} />
                {item.label}
              </button>
            );
          })}
        </nav>
        <div className="sidebar-footer">
          <button type="button" onClick={() => setActiveTab('settings')}><Settings size={16} />{t('settings')}</button>
          <button type="button"><CircleHelp size={16} />{t('help')}</button>
          <div className="mini-profile">
            <span className="avatar">MD</span>
            <span>Mina Dilek</span>
          </div>
        </div>
      </aside>

      <section className="main-area">
        <header className="top-bar">
          <div>
            <h1>{activeTab === 'config' ? t('configAnalysis') : activeTab === 'rules' ? t('rules') : activeTab === 'flowchartConverter' ? t('flowchartConverter') : activeTab === 'processPdd' ? t('processPddTitle') : analysis ? analysis.projectName ?? t('projectOverview') : t('dashboard')}</h1>
            <p>{activeTab === 'config' ? t('configIntelligenceHelp') : activeTab === 'rules' ? t('rulesWorkspaceHelp') : activeTab === 'flowchartConverter' ? t('flowchartConverterHelp') : activeTab === 'processPdd' ? t('processPddHelp') : analysis ? t('projectWorkspace') : t('homeSubtitle')}</p>
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

        {activeTab === 'rules' && ruleNotification && (
          <div className="app-toast" role="alert" key={ruleNotification.id}>
            <span>{ruleNotification.message}</span>
            <button
              type="button"
              aria-label={t('dismissNotification')}
              onClick={() => {
                setRuleNotification(null);
                setRuleStatus('');
              }}
            >
              x
            </button>
          </div>
        )}

        <section className="workspace">
          {activeTab !== 'rules' && activeTab !== 'flowchartConverter' && activeTab !== 'processPdd' && (
            <section className={`project-card${activeTab === 'config' ? ' config-project-card' : ''}`} aria-label="Project intake">
              <div className="project-card-header">
                <div>
                  <span className="eyebrow">{t('projectIntake')}</span>
                  <h2>{t('uipathProject')}</h2>
                </div>
                {activeTab !== 'config' && <span className={`health ${healthState}`}>{statusMessage}</span>}
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
                  {projectPath.trim() && <p className="hint">{t('projectProfileHint')}</p>}
                </div>
              </div>
              {activeTab !== 'config' && !desktop && <p className="hint">{t('browserModeHint')}</p>}
              <div className={activeTab === 'config' ? 'config-project-footer' : undefined}>
                <div className="validation-row">
                  <span className={selectedFolder || projectPath.trim() ? 'status-dot ready' : 'status-dot'} />
                  <span>{selectedFolder ? t('folderSelected') : projectPath.trim() ? t('manualPathEntered') : t('noFolderSelected')}</span>
                </div>
                <div className="action-row">
                  <button className="primary-action" type="button" onClick={runAnalysis} disabled={isAnalyzing || healthState !== 'ready'}>
                    <Play size={18} />
                    {isAnalyzing ? t('analyzing') : t('analyzeProject')}
                  </button>
                  {activeTab !== 'config' && (
                    <>
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
                    </>
                  )}
                </div>
              </div>
            </section>
          )}

          {activeTab === 'settings' ? (
            <SettingsView section={settingsSection} language={language} t={t} onSectionChange={setSettingsSection} onLanguageChange={setLanguage} />
          ) : activeTab === 'config' ? (
            <ConfigAnalysisView projectPath={projectPath.trim()} desktop={desktop} t={t} />
          ) : activeTab === 'flowchartConverter' ? (
            <FlowchartConverterView desktop={desktop} t={t} />
          ) : activeTab === 'rules' ? (
            <RulesView
              rules={rules}
              isLoading={rulesLoading}
              selectedRule={selectedRule}
              projectPath={projectPath.trim()}
              profiles={profiles}
              selectedProfileId={selectedProfileId}
              status={ruleStatus}
              t={t}
              onSelectRule={setSelectedRule}
              onRefresh={() => {
                void refreshRules();
                void refreshProfiles();
              }}
              onSelectProfileId={setSelectedProfileId}
              onStatus={(message) => {
                setRuleStatus(message);
                if (message) {
                  setRuleNotification({ id: Date.now(), message });
                }
              }}
            />
          ) : activeTab === 'processPdd' ? (
            <ProcessPddAnalysisView
              analysis={analysis}
              projectPath={projectPath.trim()}
              desktop={desktop}
              locale={language}
              t={t}
            />
          ) : analysis ? (
            <>
              <nav className="tabs" aria-label="Analysis sections">
                {(['overview', 'findings', 'history', 'workflows', 'dependencies', 'report', 'ask'] as const).map((tab) => (
                  <button
                    key={tab}
                    type="button"
                    className={activeTab === tab ? 'active' : ''}
                    onClick={() => setActiveTab(tab)}
                  >
                    {tab === 'overview' ? t('overview')
                      : tab === 'findings' ? t('findingsNav')
                        : tab === 'history' ? t('changeHistory')
                            : tab === 'workflows' ? t('workflows')
                              : tab === 'dependencies' ? t('dependencies')
                                : tab === 'report' ? t('report')
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
                    ruleFilter={findingRuleFilter}
                    query={findingQuery}
                    categories={findingCategories}
                    rules={findingRules}
                    onSeverityFilterChange={setFindingSeverityFilter}
                    onCategoryFilterChange={setFindingCategoryFilter}
                    onRuleFilterChange={setFindingRuleFilter}
                    onQueryChange={setFindingQuery}
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
                    currentProjectPath={projectPath.trim()}
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
                    onProjectChanged={() => {
                      setAnalysisOutOfDate(true);
                      void refreshBackups();
                    }}
                  />
                )}
                {activeTab === 'dependencies' && <DependenciesView dependencyAnalysis={analysis.dependencyAnalysis ?? null} findings={findings} locale={language} t={t} />}
                {activeTab === 'report' && <ReportView analysis={analysis} topIssues={topIssues} workflows={workflowHealth} locale={language} t={t} />}
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

const rootElement = document.getElementById('root');
if (rootElement) {
  ReactDOM.createRoot(rootElement).render(<App />);
}

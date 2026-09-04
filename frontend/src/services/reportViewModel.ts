import type { ReportFormat } from './reportExportService';

export interface AnalysisResponse {
  projectName?: string | null;
  projectPath?: string;
  compatibility?: string | null;
  workflowCount?: number;
  totalActivityCount?: number;
  analysis?: {
    errorCount?: number;
    warningCount?: number;
    suggestionCount?: number;
    infoCount?: number;
    totalOccurrences?: number;
    findings?: Finding[];
  };
  qualityScore?: {
    score?: number;
    grade?: string;
    profileId?: string;
    profileName?: string;
    rawPenalty?: number;
    normalizedPenalty?: number;
    scoreBreakdown?: ScoreBreakdown[];
    severityBreakdown?: SeverityBreakdown[];
    projectSizeFactor?: number;
  };
  workflows?: Workflow[];
  projectScan?: {
    workflows?: Workflow[];
  };
}

export interface RuleCatalogItem {
  id: string;
  name: string;
  description?: string | null;
  recommendation?: string | null;
  category: string;
  defaultSeverity: string;
  scope: 'Activity' | 'Workflow' | 'Project' | string;
  source?: string;
  enabledByDefault?: boolean;
  isBuiltIn?: boolean;
  isCustom?: boolean;
  hasFixSuggestion?: boolean;
  canAutoApply?: boolean;
  supportsAggregation?: boolean;
  defaultWeight?: number;
  defaultMaxPenalty?: number;
  tags?: string[];
}

export interface CustomRuleCondition {
  field: string;
  operator: string;
  propertyName?: string | null;
  value?: string | null;
  compareValue?: string | null;
  caseSensitive?: boolean;
}

export interface CustomRuleDefinition {
  id: string;
  name: string;
  nameTr?: string | null;
  nameEn?: string | null;
  description?: string | null;
  descriptionTr?: string | null;
  descriptionEn?: string | null;
  recommendation?: string | null;
  recommendationTr?: string | null;
  recommendationEn?: string | null;
  category: string;
  severity: string;
  scope: 'Activity' | 'Workflow' | 'Project' | string;
  enabled: boolean;
  weight: number;
  maxPenalty: number;
  matchMode: 'All' | 'Any' | string;
  conditions: CustomRuleCondition[];
}

export interface RuleConfiguration {
  ruleId: string;
  enabled: boolean;
  severityOverride?: string | null;
  weight: number;
  maxPenalty: number;
  description?: string | null;
}

export interface RuleProfile {
  id: string;
  name: string;
  description?: string | null;
  rules: RuleConfiguration[];
}

export interface CustomRuleTestResult {
  ruleId: string;
  matchedWorkflowCount: number;
  matchedActivityCount: number;
  estimatedFindingCount: number;
  hasNoiseWarning?: boolean;
  noiseWarning?: string | null;
  matchedWorkflows?: string[];
  matchedActivities?: Array<{
    activityId?: string | null;
    activityName?: string | null;
    activityDisplayName?: string | null;
    propertyName?: string | null;
    currentValue?: string | null;
  }>;
}

export interface ScoreBreakdown {
  ruleId?: string;
  ruleName?: string;
  findingCount?: number;
  occurrenceCount?: number;
  severity?: string;
  weight?: number;
  rawPenalty?: number;
  appliedPenalty?: number;
  maxPenalty?: number;
}

export interface SeverityBreakdown {
  severity?: string;
  findingCount?: number;
  occurrenceCount?: number;
  rawPenalty?: number;
  appliedPenalty?: number;
}

export interface AiReviewResult {
  isSuccess?: boolean;
  summary: string;
  riskLevel: string;
  strengths?: string[];
  issues?: AiReviewIssue[];
  recommendations?: string[];
  architectureObservations?: string[];
  confidence?: number;
  reviewedScope?: string;
  reviewedWorkflowPath?: string | null;
  model?: string | null;
  generatedAtUtc?: string;
  errorMessage?: string | null;
}

export interface AiReviewIssue {
  title: string;
  severity: string;
  description: string;
  evidence: string;
  recommendation: string;
  workflowPath?: string | null;
  relatedRuleIds?: string[];
}

export interface ProjectAnswer {
  answer: string;
  confidence: 'Low' | 'Medium' | 'High' | string;
  answerType: 'Direct' | 'Aggregated' | 'Analytical' | 'InsufficientEvidence' | string;
  evidence?: ProjectEvidence[];
  relatedWorkflows?: string[];
  relatedActivities?: string[];
  relatedRuleIds?: string[];
  usedAi: boolean;
  model?: string | null;
  generatedAtUtc?: string;
  reasoningSummary?: string | null;
  errorMessage?: string | null;
}

export interface ProjectEvidence {
  type: string;
  workflowPath?: string | null;
  activityName?: string | null;
  activityDisplayName?: string | null;
  ruleId?: string | null;
  propertyName?: string | null;
  value?: string | null;
  description?: string | null;
  relevanceScore?: number;
}

export interface Finding {
  ruleId: string;
  ruleName: string;
  severity: string;
  category?: string;
  workflowPath?: string;
  activityId?: string | null;
  activityName?: string | null;
  activityDisplayName?: string;
  propertyName?: string | null;
  message: string;
  description?: string | null;
  recommendation?: string | null;
  currentValue?: string | null;
  source?: string;
  scope?: string;
  occurrenceCount?: number;
  affectedActivityCount?: number | null;
  totalRelevantActivityCount?: number | null;
  percentage?: number | null;
  exampleActivities?: AffectedActivity[];
  affectedActivities?: AffectedActivity[];
}

export interface AffectedActivity {
  activityId?: string | null;
  stableId?: string | null;
  activityPath?: string | null;
  activityName: string;
  activityDisplayName: string;
  propertyName?: string | null;
  currentValue?: string | null;
}

export interface FixSuggestionResult {
  isAvailable?: boolean;
  suggestion?: FixSuggestion | null;
  message?: string | null;
  validation?: {
    isValid?: boolean;
    errors?: string[];
    warnings?: string[];
  } | null;
}

export interface FixSuggestion {
  id: string;
  fixId?: string;
  ruleId: string;
  title: string;
  description: string;
  fixType: string;
  fixability?: string;
  confidence: string;
  riskLevel: string;
  workflowPath?: string | null;
  activityId?: string | null;
  activityName?: string | null;
  activityDisplayName?: string | null;
  propertyName?: string | null;
  currentValue?: string | null;
  currentState?: string | null;
  suggestedValue?: string | null;
  proposedState?: string | null;
  beforePreview?: string | null;
  afterPreview?: string | null;
  patchPreview?: {
    format: string;
    workflowPath?: string | null;
    description?: string | null;
    before?: string | null;
    after?: string | null;
    changedProperties?: Array<{ name: string; before?: string | null; after?: string | null }>;
    notes?: string[];
  } | null;
  explanation: string;
  validationNotes?: string[];
  steps?: string[];
  risks?: string[];
  requiresUserInput?: boolean;
  userInputHints?: string[];
  requiresAi: boolean;
  canAutoApply: boolean;
  expectedFileHash?: string | null;
  generatedAtUtc?: string;
  errorMessage?: string | null;
}

export interface FixApplyResult {
  success: boolean;
  applied: boolean;
  message: string;
  workflowPath?: string | null;
  ruleId?: string | null;
  propertyName?: string | null;
  previousValue?: string | null;
  newValue?: string | null;
  backupPath?: string | null;
  backupId?: string | null;
  validationResult?: {
    isValid?: boolean;
    errors?: string[];
    warnings?: string[];
  };
  appliedAtUtc?: string | null;
  errorCode?: string | null;
  requiresReanalysis?: boolean;
}

export interface BackupSummary {
  backupId: string;
  createdAtUtc?: string | null;
  workflowPath?: string | null;
  ruleId?: string | null;
  propertyName?: string | null;
  previousValue?: string | null;
  newValue?: string | null;
  originalHash?: string | null;
  modifiedHash?: string | null;
  status: string;
  canUndo: boolean;
  reason?: string | null;
}

export interface BackupListResult {
  backups: BackupSummary[];
}

export interface UndoResult {
  success: boolean;
  restored: boolean;
  message: string;
  backupId?: string | null;
  workflowPath?: string | null;
  previousHash?: string | null;
  restoredHash?: string | null;
  safetyBackupId?: string | null;
  restoredAtUtc?: string | null;
  requiresReanalysis?: boolean;
  errorCode?: string | null;
  validationResult?: {
    isValid?: boolean;
    errors?: string[];
    warnings?: string[];
  };
}

export interface Workflow {
  relativePath: string;
  activityCount?: number;
  complexity?: WorkflowComplexity | null;
  activities?: Activity[];
  arguments?: WorkflowArgument[];
  parseErrors?: string[];
  analysis?: {
    activityCount?: number;
  };
}

export interface WorkflowComplexity {
  workflowPath?: string;
  totalActivities?: number;
  executableActivities?: number;
  executableActivityCount?: number;
  containerActivities?: number;
  containerActivityCount?: number;
  maxNestingDepth?: number;
  decisionCount?: number;
  ifCount?: number;
  switchCount?: number;
  loopCount?: number;
  tryCatchCount?: number;
  invokeWorkflowCount?: number;
  argumentCount?: number;
  findingCount?: number;
  complexityScore?: number;
  complexityLevel?: string;
}

export interface WorkflowArgument {
  name: string;
  direction?: string | null;
  type?: string | null;
}

export interface Activity {
  activityId: string;
  parentActivityId?: string | null;
  stableId?: string | null;
  activityPath?: string | null;
  name: string;
  displayName: string;
  typeName?: string | null;
  namespace?: string | null;
  depth?: number;
  xamlFile?: string | null;
  properties?: Record<string, string | null>;
  arguments?: Record<string, string | null>;
}

const severityOrder: Record<string, number> = {
  Critical: 0,
  Error: 1,
  Warning: 2,
  Suggestion: 3,
  Info: 4,
};

export function getTopIssues(findings: Finding[], count = 5): Finding[] {
  return [...findings]
    .sort((left, right) => {
      const severityComparison = (severityOrder[left.severity] ?? 99) - (severityOrder[right.severity] ?? 99);
      if (severityComparison !== 0) {
        return severityComparison;
      }

      return `${left.workflowPath ?? ''}${left.ruleId}${left.activityDisplayName ?? ''}`.localeCompare(`${right.workflowPath ?? ''}${right.ruleId}${right.activityDisplayName ?? ''}`);
    })
    .slice(0, count);
}

export function getWorkflowHealth(analysis: AnalysisResponse): Array<{ relativePath: string; findingCount: number; activityCount: number; complexity?: WorkflowComplexity | null; workflow: Workflow }> {
  const findings = analysis.analysis?.findings ?? [];
  const workflows = analysis.workflows ?? analysis.projectScan?.workflows ?? [];
  return workflows
    .map((workflow) => ({
      relativePath: workflow.relativePath,
      findingCount: findings.filter((finding) => finding.workflowPath === workflow.relativePath).length,
      activityCount: workflow.activityCount ?? workflow.analysis?.activityCount ?? 0,
      complexity: workflow.complexity,
      workflow,
    }))
    .sort((left, right) => right.findingCount - left.findingCount || left.relativePath.localeCompare(right.relativePath));
}

export function getComplexityDistribution(analysis: AnalysisResponse): Record<string, number> {
  const workflows = analysis.workflows ?? analysis.projectScan?.workflows ?? [];
  return workflows.reduce<Record<string, number>>((distribution, workflow) => {
    const level = workflow.complexity?.complexityLevel;
    if (level) {
      distribution[level] = (distribution[level] ?? 0) + 1;
    }

    return distribution;
  }, {});
}

export function getTopComplexWorkflows(analysis: AnalysisResponse, count = 10): Array<{ relativePath: string; complexity: WorkflowComplexity; workflow: Workflow }> {
  const workflows = analysis.workflows ?? analysis.projectScan?.workflows ?? [];
  return workflows
    .filter((workflow): workflow is Workflow & { complexity: WorkflowComplexity } => Boolean(workflow.complexity))
    .map((workflow) => ({ relativePath: workflow.relativePath, complexity: workflow.complexity, workflow }))
    .sort((left, right) => (right.complexity.complexityScore ?? 0) - (left.complexity.complexityScore ?? 0) || left.relativePath.localeCompare(right.relativePath))
    .slice(0, count);
}

export function isReportFormat(value: string): value is ReportFormat {
  return value === 'json' || value === 'html';
}

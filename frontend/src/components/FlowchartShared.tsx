import React from 'react';
import type {
  FlowchartConversionApplyResult,
  FlowchartConversionResult,
  FlowchartConversionRollbackResult,
  FlowchartPreviewNode,
} from '../services/reportViewModel';
import { Metric } from './uiUtils';

export function FlowchartConversionPanel({
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
        <Metric
          label={t('convertibility')}
          value={localizeFlowchartLevel(result.assessment?.conversionLevel, t)}
        />
        <Metric
          label={t('confidence')}
          value={localizeFlowchartConfidence(result.assessment?.confidence, t)}
        />
        <Metric label={t('nodes')} value={result.graph?.nodes.length ?? 0} />
        <Metric label={t('decisions')} value={result.graph?.decisions.length ?? 0} />
        <Metric label={t('switches')} value={result.graph?.switches.length ?? 0} />
        <Metric label={t('cycles')} value={result.graph?.hasCycles ? t('yes') : t('no')} />
        <Metric
          label={t('unreachableNodes')}
          value={result.graph?.hasUnreachableNodes ? t('yes') : t('no')}
        />
      </div>

      <div className="before-after-grid">
        <section>
          <h3>{t('currentFlowchart')}</h3>
          <ul className="compact-list">
            {(result.graph?.nodes ?? []).slice(0, 12).map((node) => (
              <li key={node.id}>
                {node.id} · {node.type} · {node.displayName ?? node.activityName ?? '-'}
              </li>
            ))}
          </ul>
        </section>
        <section>
          <h3>{t('proposedSequence')}</h3>
          {result.plan?.previewTree ? (
            <PreviewTree node={result.plan.previewTree} />
          ) : (
            <p className="empty-state">{t('noPreviewAvailable')}</p>
          )}
        </section>
      </div>

      <details open>
        <summary>{t('risks')}</summary>
        {(result.assessment?.risks.length ?? 0) === 0 ? (
          <p className="empty-state">{t('noRisks')}</p>
        ) : (
          <ul>
            {result.assessment?.risks.map((risk) => (
              <li key={risk}>{risk}</li>
            ))}
          </ul>
        )}
      </details>
      <details>
        <summary>{t('unsupportedPatterns')}</summary>
        {(result.assessment?.unsupportedPatterns.length ?? 0) === 0 ? (
          <p className="empty-state">{t('noUnsupportedPatterns')}</p>
        ) : (
          <ul>
            {result.assessment?.unsupportedPatterns.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        )}
      </details>
      <details>
        <summary>{t('conversionMappings')}</summary>
        <table className="compact-table">
          <thead>
            <tr>
              <th>{t('sourceNode')}</th>
              <th>{t('targetPath')}</th>
              <th>{t('type')}</th>
            </tr>
          </thead>
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
        <p className="empty-state">
          {canApply ? t('conversionApplyWarning') : t('conversionManualReviewOnly')}
        </p>
        <div className="conversion-actions">
          {canApply ? (
            <button
              type="button"
              onClick={onApply}
              disabled={isApplying || applyResult?.applied === true}
            >
              {isApplying ? t('applying') : t('applyConversion')}
            </button>
          ) : (
            <span className="status-badge review">{t('manualReviewRequired')}</span>
          )}
          {applyResult?.rollbackAvailable && applyResult.backupId && (
            <button
              type="button"
              onClick={onRollback}
              disabled={isRollingBack || rollbackResult?.restored === true}
            >
              {isRollingBack ? t('rollingBack') : t('rollback')}
            </button>
          )}
        </div>
        {applyResult && (
          <div className={`notice ${applyResult.success ? 'success' : 'error'}`}>
            <strong>
              {applyResult.success
                ? t('conversionAppliedSuccessfully')
                : t('conversionWasNotApplied')}
            </strong>
            <p>{applyResult.message}</p>
            {applyResult.backupId && (
              <p>
                {t('backupCreated')}: {applyResult.backupId}
              </p>
            )}
            {applyResult.requiresReanalysis && (
              <p>{t('projectFilesChangedReanalysis')}</p>
            )}
          </div>
        )}
        {rollbackResult && (
          <div className={`notice ${rollbackResult.success ? 'success' : 'error'}`}>
            <strong>
              {rollbackResult.success
                ? t('conversionRolledBackSuccessfully')
                : t('conversionRollbackFailed')}
            </strong>
            <p>{rollbackResult.message}</p>
            {rollbackResult.safetyBackupId && (
              <p>
                {t('safetyBackupCreated')}: {rollbackResult.safetyBackupId}
              </p>
            )}
            {rollbackResult.requiresReanalysis && (
              <p>{t('projectFilesChangedReanalysis')}</p>
            )}
          </div>
        )}
      </section>
    </div>
  );
}

export function FlowchartConversionConfirmDialog({
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
    <div
      className="modal-backdrop"
      role="dialog"
      aria-modal="true"
      aria-label={t('applyConversion')}
    >
      <div className="modal-card">
        <h2>{t('applyConversion')}</h2>
        <p>{t('conversionApplyWarning')}</p>
        <div className="kv">
          <div>Workflow</div>
          <div>{workflowPath}</div>
          <div>{t('currentStructure')}</div>
          <div>Flowchart</div>
          <div>{t('proposedSequence')}</div>
          <div>Sequence</div>
        </div>
        <p className="empty-state">{t('conversionBackupWillBeCreated')}</p>
        <div className="modal-actions">
          <button type="button" onClick={onCancel} disabled={isApplying}>
            {t('cancel')}
          </button>
          <button type="button" onClick={onConfirm} disabled={isApplying}>
            {isApplying ? t('applying') : t('confirmConversion')}
          </button>
        </div>
      </div>
    </div>
  );
}

export function PreviewTree({ node }: { node: FlowchartPreviewNode }) {
  const label = [node.type, node.displayName, node.condition ? `[${node.condition}]` : null]
    .filter(Boolean)
    .join(' - ');
  return (
    <ul className="activity-tree">
      <li>
        <span>{label}</span>
        {(node.children?.length ?? 0) > 0 && (
          <ul>
            {node.children?.map((child, index) => (
              <PreviewTree
                key={`${child.sourceNodeId ?? child.type}-${index}`}
                node={child}
              />
            ))}
          </ul>
        )}
      </li>
    </ul>
  );
}

export function formatConversionPlan(
  result: FlowchartConversionResult,
  t: (key: string, values?: Record<string, unknown>) => string,
): string {
  const lines = [
    `${t('flowchartConversion')}: ${result.workflowPath}`,
    `${t('currentStructure')}: ${result.structureType}`,
    `${t('convertibility')}: ${localizeFlowchartLevel(result.assessment?.conversionLevel, t)}`,
    `${t('confidence')}: ${localizeFlowchartConfidence(result.assessment?.confidence, t)}`,
    '',
    t('steps'),
    ...(result.plan?.steps ?? []).map((step) => `- ${localizeFlowchartNarrative(step, t)}`),
    '',
    t('risks'),
    ...((result.assessment?.risks.length ?? 0) === 0
      ? [`- ${t('noRisks')}`]
      : result.assessment!.risks.map((risk) => `- ${localizeFlowchartNarrative(risk, t)}`)),
  ];
  return lines.join('\n');
}

export function localizeFlowchartNarrative(
  value: string,
  t: (key: string, values?: Record<string, unknown>) => string,
): string {
  const exactKeys: Record<string, string> = {
    'The Flowchart is linear and has no cycles or unreachable nodes.': 'flowchartReasonLinear',
    'The Flowchart has simple acyclic decision structure.': 'flowchartReasonSimpleDecision',
    'The Flowchart can be previewed, but branch/merge behavior should be reviewed in UiPath Studio.':
      'flowchartReasonReviewBranches',
    'The Flowchart contains loop/back-edge control flow and requires careful review after conversion.':
      'flowchartReasonLoopReview',
    'Cycle detected. Flowchart cycles can represent while, retry, or goto-like control flow.':
      'flowchartRiskCycle',
    'Unreachable node detected from the Flowchart start node.': 'flowchartRiskUnreachable',
    'Create a Sequence root.': 'flowchartStepCreateSequence',
    'Preserve workflow arguments, variables, activity properties, and expressions.':
      'flowchartStepPreserveContent',
    'Move linear FlowStep activities into Sequence order.': 'flowchartStepMoveActivities',
    'Represent FlowDecision branches as If preview nodes.': 'flowchartStepDecision',
    'Represent FlowSwitch branches as Switch preview nodes.': 'flowchartStepSwitch',
    'Keep shared merge nodes as continuation activities after branch previews.':
      'flowchartStepMerge',
    'Recognized back-edge loops can be represented as While preview nodes and must be reviewed in UiPath Studio.':
      'flowchartStepLoop',
    'Exclude commented-out activity blocks from the generated Sequence.':
      'flowchartStepExcludeComments',
    'Nested Flowchart detected inside a non-Flowchart workflow.': 'flowchartNestedDetected',
    'Automatic conversion currently supports only workflows whose root activity is Flowchart.':
      'flowchartRootOnlySupported',
  };
  const key = exactKeys[value];
  if (key) {
    return t(key);
  }

  const patterns: Array<{ expression: RegExp; key: string; valueName: string }> = [
    {
      expression: /^Multiple entry-like nodes detected: (\d+)\.$/,
      key: 'flowchartRiskMultipleEntries',
      valueName: 'count',
    },
    {
      expression: /^FlowDecision branch targets could not be resolved for node: (.+)\.$/,
      key: 'flowchartUnsupportedDecisionTarget',
      valueName: 'node',
    },
    {
      expression: /^FlowSwitch case targets could not be resolved for node: (.+)\.$/,
      key: 'flowchartUnsupportedSwitchTarget',
      valueName: 'node',
    },
    {
      expression: /^Unsupported flow node type count: (\d+)\.$/,
      key: 'flowchartUnsupportedNodeCount',
      valueName: 'count',
    },
    {
      expression: /^Nested Flowchart detected inside a (.+) workflow\..*$/,
      key: 'flowchartNestedMessage',
      valueName: 'structure',
    },
  ];
  for (const pattern of patterns) {
    const match = value.match(pattern.expression);
    if (match) {
      return t(pattern.key, { [pattern.valueName]: match[1] });
    }
  }

  if (value === 'No flow nodes were parsed.') {
    return t('flowchartUnsupportedNoNodes');
  }

  return value;
}

export function localizeFlowchartLevel(
  value: string | null | undefined,
  t: (key: string, values?: Record<string, unknown>) => string,
): string {
  if (!value) {
    return t('unknown');
  }

  return t(`flowchartLevel${value.replace(/\s/g, '')}`);
}

export function localizeFlowchartConfidence(
  value: string | null | undefined,
  t: (key: string, values?: Record<string, unknown>) => string,
): string {
  if (!value) {
    return t('unknown');
  }

  return t(`flowchartConfidence${value.replace(/\s/g, '')}`);
}

export function defaultConvertedPath(sourcePath: string, suggestedFileName?: string): string {
  const normalized = sourcePath.replace(/\\/g, '/');
  const directory = normalized.includes('/') ? normalized.slice(0, normalized.lastIndexOf('/')) : '';
  const fileName = suggestedFileName || `${getFileName(sourcePath).replace(/\.xaml$/i, '')}_Sequence.xaml`;
  return directory ? `${directory}/${fileName}` : fileName;
}

export function localizeStandaloneFlowchartStatus(
  status: string | null | undefined,
  t: (key: string, values?: Record<string, unknown>) => string,
): string {
  if (!status) {
    return t('unknown');
  }

  return t(`standaloneFlowchartStatus${status}`);
}

export function flowchartStandaloneStatusClass(status: string | null | undefined): string {
  if (status === 'Ready' || status === 'Converted' || status === 'AlreadySequence') {
    return 'good';
  }

  if (status === 'RequiresReview' || status === 'Complex' || status === 'Unsupported') {
    return 'review';
  }

  return 'risk';
}

export function mergeStandaloneResults(
  current: any[],
  next: any[],
): any[] {
  const byPath = new Map(current.map((item) => [item.filePath, item]));
  for (const item of next) {
    byPath.set(item.filePath, item);
  }

  return Array.from(byPath.values());
}

function getFileName(path: string): string {
  return path.replaceAll('\\', '/').split('/').pop() ?? path;
}

export function copyText(text: string): void {
  void navigator.clipboard?.writeText(text);
}

import React, { useState, useMemo } from 'react';
import {
  Activity,
  Layers,
  FileText,
  GitBranch,
  MessageSquare,
  RotateCw,
  PlayCircle,
  Clock,
  Check,
  Copy,
  ChevronDown,
  ChevronRight,
  ArrowDown,
  Box,
  CornerDownRight,
  AlertTriangle,
  ShieldCheck,
  Sparkles,
} from 'lucide-react';
import type { FlowchartPreviewNode, CustomActivityDetection } from '../services/reportViewModel';

interface ActivityInfo {
  id: string;
  type: string;
  displayName: string;
  sourceNodeId?: string;
  condition?: string;
  role: 'container' | 'executable' | 'controlFlow';
  depth: number;
}

function getActivityIcon(type: string) {
  const lower = type.toLowerCase();
  if (lower.includes('sequence') || lower.includes('flowchart')) {
    return <Layers size={16} className="activity-icon-container" />;
  }
  if (lower.includes('assign')) {
    return <FileText size={16} className="activity-icon-assign" />;
  }
  if (lower.includes('if') || lower.includes('decision') || lower.includes('switch')) {
    return <GitBranch size={16} className="activity-icon-branch" />;
  }
  if (lower.includes('log') || lower.includes('write')) {
    return <MessageSquare size={16} className="activity-icon-log" />;
  }
  if (lower.includes('while') || lower.includes('loop') || lower.includes('repeat')) {
    return <RotateCw size={16} className="activity-icon-loop" />;
  }
  if (lower.includes('invoke')) {
    return <PlayCircle size={16} className="activity-icon-invoke" />;
  }
  if (lower.includes('delay')) {
    return <Clock size={16} className="activity-icon-delay" />;
  }
  return <Activity size={16} className="activity-icon-default" />;
}

function getActivityRole(type: string): 'container' | 'executable' | 'controlFlow' {
  const lower = type.toLowerCase();
  if (lower.includes('sequence') || lower.includes('flowchart') || lower.includes('trycatch')) {
    return 'container';
  }
  if (lower.includes('if') || lower.includes('decision') || lower.includes('switch') || lower.includes('while')) {
    return 'controlFlow';
  }
  return 'executable';
}

function isCommentedNode(node: FlowchartPreviewNode): boolean {
  const typeLower = node.type.toLowerCase();
  const displayLower = (node.displayName || '').toLowerCase().trim();
  return (
    typeLower.includes('comment') ||
    displayLower.startsWith('//') ||
    displayLower.startsWith('/*') ||
    displayLower.startsWith('<!--') ||
    displayLower === 'comment' ||
    displayLower === 'commentout' ||
    displayLower === 'comment out' ||
    displayLower.startsWith('disabled')
  );
}

function collectActivities(
  node: FlowchartPreviewNode,
  depth = 0,
  customDetectionsMap?: Map<string, CustomActivityDetection>,
  replaceCustomActivities = false,
): ActivityInfo[] {
  if (isCommentedNode(node)) {
    return [];
  }

  let effectiveType = node.type;
  let effectiveDisplayName = node.displayName || node.type;
  if (replaceCustomActivities && customDetectionsMap && node.sourceNodeId) {
    const detection = customDetectionsMap.get(node.sourceNodeId);
    if (detection) {
      effectiveType = detection.suggestedUiPathActivity;
      effectiveDisplayName = detection.displayName || node.displayName || detection.suggestedUiPathActivity;
    }
  }

  const items: ActivityInfo[] = [];
  const current: ActivityInfo = {
    id: node.sourceNodeId || `${effectiveType}-${items.length}-${depth}`,
    type: effectiveType,
    displayName: effectiveDisplayName,
    sourceNodeId: node.sourceNodeId ?? undefined,
    condition: node.condition ?? undefined,
    role: getActivityRole(effectiveType),
    depth,
  };
  items.push(current);

  if (node.children) {
    for (const child of node.children) {
      if (!isCommentedNode(child)) {
        items.push(...collectActivities(child, depth + 1, customDetectionsMap, replaceCustomActivities));
      }
    }
  }
  return items;
}

function escapeXml(unsafe: string): string {
  return unsafe
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;');
}

export function generateXamlSnippet(
  node: FlowchartPreviewNode,
  indentLevel = 0,
  customDetectionsMap?: Map<string, CustomActivityDetection>,
  replaceCustomActivities = false,
): string {
  if (isCommentedNode(node)) {
    return '';
  }

  const indent = '  '.repeat(indentLevel);
  let rawType = node.type;
  let rawDisplayName = node.displayName;

  if (replaceCustomActivities && customDetectionsMap && node.sourceNodeId) {
    const detection = customDetectionsMap.get(node.sourceNodeId);
    if (detection) {
      rawType = detection.suggestedUiPathActivity;
      rawDisplayName = detection.displayName || node.displayName || detection.suggestedUiPathActivity;
    }
  }

  const tag = rawType.includes(':')
    ? rawType
    : rawType === 'LogMessage'
    ? 'ui:LogMessage'
    : rawType;

  const attrs: string[] = [];
  if (indentLevel === 0) {
    attrs.push('xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"');
    attrs.push('xmlns:ui="http://schemas.uipath.com/workflow/activities"');
    attrs.push('xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"');
  }

  if (rawDisplayName) {
    attrs.push(`DisplayName="${escapeXml(rawDisplayName)}"`);
  }
  if (node.condition) {
    attrs.push(`Condition="[${escapeXml(node.condition)}]"`);
  }

  const attrStr = attrs.length > 0 ? ` ${attrs.join(' ')}` : '';
  const validChildren = (node.children ?? []).filter((child) => !isCommentedNode(child));

  if (validChildren.length === 0) {
    return `${indent}<${tag}${attrStr} />\n`;
  }

  let xaml = `${indent}<${tag}${attrStr}>\n`;
  for (const child of validChildren) {
    xaml += generateXamlSnippet(child, indentLevel + 1, customDetectionsMap, replaceCustomActivities);
  }
  xaml += `${indent}</${tag}>\n`;
  return xaml;
}

/**
 * Custom Dependency Notice and Replacement Suggestion Card
 */
export function CustomActivityNoticeCard({
  detections,
  replaceCustomActivities,
  onToggleReplace,
  t,
}: {
  detections: CustomActivityDetection[];
  replaceCustomActivities: boolean;
  onToggleReplace: (replace: boolean) => void;
  t: (key: string, values?: Record<string, unknown>) => string;
}) {
  if (!detections || detections.length === 0) {
    return null;
  }

  return (
    <div className="custom-dependency-notice-card" data-testid="custom-dependency-notice">
      <div className="custom-dependency-header">
        <div className="custom-dependency-title-group">
          <AlertTriangle className="text-amber-500" size={20} />
          <div>
            <h4 className="custom-dependency-heading">{t('customDependencyDetected')}</h4>
            <p className="custom-dependency-subtext">
              {t('customDependencyNotice', { count: detections.length })}
            </p>
          </div>
        </div>

        <label className="custom-dependency-toggle-label">
          <input
            type="checkbox"
            className="custom-dependency-checkbox"
            checked={replaceCustomActivities}
            onChange={(e) => onToggleReplace(e.target.checked)}
            data-testid="toggle-replace-custom-activities"
          />
          <span className="toggle-label-text">
            <Sparkles size={15} className="inline-sparkle text-primary" />
            <strong>{t('replaceWithStandardUiPath')}</strong>
          </span>
        </label>
      </div>

      <div className="table-scroll custom-activities-table-wrapper">
        <table className="compact-table custom-activities-table">
          <thead>
            <tr>
              <th>{t('customActivityName')}</th>
              <th>Node ID</th>
              <th>{t('suggestedReplacement')}</th>
              <th>{t('suggestedPackage')}</th>
              <th>{t('replacementReason')}</th>
            </tr>
          </thead>
          <tbody>
            {detections.map((d) => (
              <tr key={d.nodeId} className={replaceCustomActivities ? 'replaced-row' : ''}>
                <td>
                  <div className="custom-act-name">
                    <code>{d.activityName}</code>
                    {d.displayName && <span className="display-name-sub">({d.displayName})</span>}
                  </div>
                </td>
                <td>
                  <span className="source-node-badge">{d.nodeId}</span>
                </td>
                <td>
                  <span className="suggested-replacement-badge">
                    <ShieldCheck size={14} className="text-emerald-500" />
                    <strong>{d.suggestedUiPathActivity}</strong>
                  </span>
                </td>
                <td>
                  <code className="pkg-name">{d.suggestedPackage}</code>
                </td>
                <td className="reason-cell">
                  <small>{d.replacementReason || '-'}</small>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {replaceCustomActivities && (
        <div className="replacement-applied-banner">
          <Check size={16} className="text-emerald-500" />
          <span>{t('replaceWithStandardUiPathHelp')}</span>
        </div>
      )}
    </div>
  );
}

/**
 * Kullanılacak Aktiviteler (Activities Breakdown)
 */
export function FlowchartActivitiesBreakdown({
  tree,
  customDetections,
  replaceCustomActivities = false,
  t,
}: {
  tree: FlowchartPreviewNode;
  customDetections?: CustomActivityDetection[];
  replaceCustomActivities?: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
}) {
  const detectionsMap = useMemo(() => {
    if (!customDetections || customDetections.length === 0) return undefined;
    return new Map(customDetections.map((d) => [d.nodeId, d]));
  }, [customDetections]);

  const activities = useMemo(
    () => collectActivities(tree, 0, detectionsMap, replaceCustomActivities),
    [tree, detectionsMap, replaceCustomActivities],
  );

  // Aggregate by activity type
  const typeCounts = useMemo(() => {
    const counts = new Map<string, number>();
    for (const a of activities) {
      counts.set(a.type, (counts.get(a.type) ?? 0) + 1);
    }
    return Array.from(counts.entries()).sort((a, b) => b[1] - a[1]);
  }, [activities]);

  return (
    <div className="activities-breakdown-card">
      <header className="activities-breakdown-header">
        <div className="activities-breakdown-title">
          <Box size={18} className="text-primary" />
          <h4>{t('activitiesToUse')}</h4>
        </div>
        <span className="activity-count-badge">
          {t('activitiesUsedCount', { count: activities.length })}
        </span>
      </header>

      {/* Summary Type Pills */}
      <div className="activity-pills-row">
        {typeCounts.map(([type, count]) => (
          <span key={type} className={`activity-type-pill pill-${type.toLowerCase().replace(/[:.]/g, '-')}`}>
            {getActivityIcon(type)}
            <strong className="pill-type-name">{type}</strong>
            <span className="pill-count">×{count}</span>
          </span>
        ))}
      </div>

      {/* Detailed Activity Table */}
      <div className="table-scroll activities-table-container">
        <table className="compact-table activities-detail-table">
          <thead>
            <tr>
              <th>#</th>
              <th>{t('type')}</th>
              <th>Aktivite Adı</th>
              <th>{t('activityRole')}</th>
              <th>{t('activitySourceNode')}</th>
            </tr>
          </thead>
          <tbody>
            {activities.map((item, index) => (
              <tr key={`${item.id}-${index}`}>
                <td className="index-cell">{index + 1}</td>
                <td className="type-cell">
                  <div className="activity-type-cell-content">
                    {getActivityIcon(item.type)}
                    <code>{item.type}</code>
                  </div>
                </td>
                <td className="name-cell">
                  <strong>{item.displayName}</strong>
                  {item.condition && (
                    <span className="condition-tag">[{item.condition}]</span>
                  )}
                </td>
                <td>
                  <span className={`role-badge role-${item.role}`}>
                    {item.role === 'container'
                      ? t('containerActivity')
                      : item.role === 'controlFlow'
                      ? t('controlFlowActivity')
                      : t('executableActivity')}
                  </span>
                </td>
                <td className="source-node-cell">
                  {item.sourceNodeId ? (
                    <span className="source-node-badge">{item.sourceNodeId}</span>
                  ) : (
                    <span className="text-muted">-</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/**
 * Görsel Akış Önizlemesi (UiPath Studio Style Visual Flow)
 */
export function FlowchartVisualSequenceFlow({
  node,
  customDetections,
  replaceCustomActivities = false,
  isRoot = true,
}: {
  node: FlowchartPreviewNode;
  customDetections?: CustomActivityDetection[];
  replaceCustomActivities?: boolean;
  isRoot?: boolean;
}) {
  if (isCommentedNode(node)) {
    return null;
  }

  const [isExpanded, setIsExpanded] = useState(true);
  const isContainer = node.type.toLowerCase().includes('sequence') || node.type.toLowerCase().includes('flowchart');
  const isBranch = node.type.toLowerCase().includes('if') || node.type.toLowerCase().includes('decision');
  const children = (node.children ?? []).filter((child) => !isCommentedNode(child));

  const detection = replaceCustomActivities && customDetections && node.sourceNodeId
    ? customDetections.find((d) => d.nodeId === node.sourceNodeId)
    : undefined;

  const displayType = detection ? detection.suggestedUiPathActivity : node.type;
  const displayName = detection ? (detection.displayName || node.displayName || detection.suggestedUiPathActivity) : (node.displayName || node.type);

  if (isContainer) {
    return (
      <div className={`visual-sequence-container ${isRoot ? 'root-container' : ''}`}>
        <div
          className="sequence-container-header"
          onClick={() => setIsExpanded(!isExpanded)}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              setIsExpanded(!isExpanded);
            }
          }}
        >
          <div className="header-left">
            {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
            <Layers size={18} className="container-icon" />
            <span className="container-type">{node.type}</span>
            <strong className="container-name">{node.displayName || 'Sequence'}</strong>
          </div>
          <div className="header-right">
            {node.sourceNodeId && (
              <span className="node-id-pill">ID: {node.sourceNodeId}</span>
            )}
            <span className="child-count-pill">{children.length} Adım</span>
          </div>
        </div>

        {isExpanded && (
          <div className="sequence-container-body">
            {children.length === 0 ? (
              <div className="empty-sequence-placeholder">Boş Sequence</div>
            ) : (
              children.map((child, index) => (
                <React.Fragment key={`${child.sourceNodeId ?? child.type}-${index}`}>
                  <FlowchartVisualSequenceFlow
                    node={child}
                    customDetections={customDetections}
                    replaceCustomActivities={replaceCustomActivities}
                    isRoot={false}
                  />
                  {index < children.length - 1 && (
                    <div className="flow-step-connector" aria-hidden="true">
                      <div className="connector-line" />
                      <ArrowDown size={14} className="connector-arrow" />
                    </div>
                  )}
                </React.Fragment>
              ))
            )}
          </div>
        )}
      </div>
    );
  }

  // Branch node (If / Decision)
  if (isBranch) {
    return (
      <div className="visual-branch-card">
        <div className="branch-card-header">
          <div className="branch-header-left">
            <GitBranch size={18} className="branch-header-icon" />
            <span className="branch-type-tag">{node.type}</span>
            <strong className="branch-display-name">{node.displayName || 'If'}</strong>
          </div>
          {node.sourceNodeId && (
            <span className="node-id-pill">ID: {node.sourceNodeId}</span>
          )}
        </div>
        {node.condition && (
          <div className="branch-condition-bar">
            <span className="condition-prefix">Koşul:</span>
            <code>{node.condition}</code>
          </div>
        )}
        {children.length > 0 && (
          <div className="branch-children-container">
            <div className="branch-lane-label">
              <CornerDownRight size={14} />
              <span>Dallanma Akışı ({children.length} Aktivite)</span>
            </div>
            <div className="branch-children-body">
              {children.map((child, index) => (
                <React.Fragment key={`${child.sourceNodeId ?? child.type}-${index}`}>
                  <FlowchartVisualSequenceFlow
                    node={child}
                    customDetections={customDetections}
                    replaceCustomActivities={replaceCustomActivities}
                    isRoot={false}
                  />
                  {index < children.length - 1 && (
                    <div className="flow-step-connector" aria-hidden="true">
                      <div className="connector-line" />
                      <ArrowDown size={14} className="connector-arrow" />
                    </div>
                  )}
                </React.Fragment>
              ))}
            </div>
          </div>
        )}
      </div>
    );
  }

  // Standard activity card (Assign, LogMessage, etc.)
  return (
    <div className={`visual-activity-card activity-card-${displayType.toLowerCase().replace(/[:.]/g, '-')}`}>
      <div className="activity-card-icon-area">
        {getActivityIcon(displayType)}
      </div>
      <div className="activity-card-details">
        <div className="activity-card-main-row">
          <span className="activity-type-label">{displayType}</span>
          <strong className="activity-display-name">{displayName}</strong>
          {detection && (
            <span className="replaced-badge" title={`Replaced from ${detection.activityName}`}>
              <ShieldCheck size={12} />
              UiPath Standard
            </span>
          )}
        </div>
        {node.condition && (
          <div className="activity-condition-snippet">
            <span>Koşul: </span>
            <code>{node.condition}</code>
          </div>
        )}
      </div>
      {node.sourceNodeId && (
        <span className="activity-source-badge" title="Orijinal Flowchart Node ID">
          {node.sourceNodeId}
        </span>
      )}
    </div>
  );
}

/**
 * XAML / Kod Bloğu Önizlemesi (Syntax Formatted Code Block)
 */
export function FlowchartXamlCodePreview({
  node,
  customDetections,
  replaceCustomActivities = false,
  t,
}: {
  node: FlowchartPreviewNode;
  customDetections?: CustomActivityDetection[];
  replaceCustomActivities?: boolean;
  t: (key: string, values?: Record<string, unknown>) => string;
}) {
  const [copied, setCopied] = useState(false);
  const detectionsMap = useMemo(() => {
    if (!customDetections || customDetections.length === 0) return undefined;
    return new Map(customDetections.map((d) => [d.nodeId, d]));
  }, [customDetections]);

  const xamlCode = useMemo(
    () => generateXamlSnippet(node, 0, detectionsMap, replaceCustomActivities),
    [node, detectionsMap, replaceCustomActivities],
  );

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(xamlCode);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback
    }
  };

  const codeLines = useMemo(() => xamlCode.trimEnd().split('\n'), [xamlCode]);

  return (
    <div className="xaml-code-preview-container">
      <div className="xaml-code-toolbar">
        <div className="code-toolbar-left">
          <span className="code-lang-badge">XAML</span>
          <span className="code-file-badge">{node.displayName || 'ConvertedSequence'}.xaml</span>
          <span className="code-line-count">{codeLines.length} satır</span>
          {replaceCustomActivities && (
            <span className="code-replaced-tag">
              <Sparkles size={13} />
              UiPath Standard Modu
            </span>
          )}
        </div>
        <button
          type="button"
          className={`code-copy-button ${copied ? 'copied' : ''}`}
          onClick={() => void handleCopy()}
          title={t('copyXamlCode')}
        >
          {copied ? (
            <>
              <Check size={15} />
              <span>{t('copiedToClipboard')}</span>
            </>
          ) : (
            <>
              <Copy size={15} />
              <span>{t('copyXamlCode')}</span>
            </>
          )}
        </button>
      </div>

      <div className="code-view-scroll">
        <pre className="code-pre">
          <table className="code-table">
            <tbody>
              {codeLines.map((line, idx) => (
                <tr key={idx} className="code-line-row">
                  <td className="code-line-num" aria-hidden="true">{idx + 1}</td>
                  <td className="code-line-content">
                    <code>{line}</code>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </pre>
      </div>
    </div>
  );
}

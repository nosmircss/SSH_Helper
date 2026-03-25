import { useCallback, useEffect, useRef, useState } from 'react';
import { blockDefMap, categoryColors, type BlockCategory, type PropertyDef } from '../blockDefs/registry';
import type { BlockNodeData } from '../nodes/BaseBlock';
import { useFlowStore } from '../stores/useFlowStore';
import { messageBus } from '../MessageBus';
import { CANVAS_HOST_MESSAGES } from '../communication-message-types';
import type { DataBlockTestResult } from '../stores/slices/executionSlice';

/**
 * Buffered text-like input state that avoids stale blur commits.
 * The latest typed value is tracked in a ref so blur cannot commit an older
 * render closure value when focus changes quickly.
 */
function useBufferedInput(externalValue: string, inputIdentity: string, onCommit: (val: string) => void) {
  const [localValue, setLocalValue] = useState(externalValue);
  const focusedRef = useRef(false);
  const latestValueRef = useRef(externalValue);
  const lastCommittedRef = useRef(externalValue);

  const commitIfNeeded = useCallback(
    (next: string) => {
      latestValueRef.current = next;
      if (next === lastCommittedRef.current) return;
      onCommit(next);
      lastCommittedRef.current = next;
    },
    [onCommit],
  );

  // Reset local buffer when we switch to a different node/field identity.
  useEffect(() => {
    focusedRef.current = false;
    latestValueRef.current = externalValue;
    lastCommittedRef.current = externalValue;
    setLocalValue(externalValue);
  }, [inputIdentity, externalValue]);

  // Apply external updates only when not focused (undo/redo/import/sync).
  useEffect(() => {
    if (focusedRef.current) return;
    latestValueRef.current = externalValue;
    lastCommittedRef.current = externalValue;
    setLocalValue(externalValue);
  }, [externalValue]);

  return {
    value: localValue,
    onChange: (next: string) => {
      latestValueRef.current = next;
      setLocalValue(next);
      commitIfNeeded(next);
    },
    onFocus: () => { focusedRef.current = true; },
    onBlur: () => {
      focusedRef.current = false;
      commitIfNeeded(latestValueRef.current);
    },
  };
}

function PropertyField({
  def,
  value,
  onChange,
  colors,
  fieldTestId,
  nodeId,
}: {
  def: PropertyDef;
  value: unknown;
  onChange: (val: unknown) => void;
  colors: { text: string; border: string; bg: string };
  fieldTestId: string;
  nodeId: string;
}) {
  const inputStyle: React.CSSProperties = {
    width: '100%',
    padding: '4px 6px',
    background: 'var(--fc-input-bg, #0d1117)',
    border: `1px solid ${colors.border}44`,
    borderRadius: 4,
    color: 'var(--fc-text, #ccc)',
    fontSize: 12,
    fontFamily: def.type === 'code' ? 'monospace' : 'inherit',
    outline: 'none',
  };

  const commitTextLikeValue = useCallback(
    (next: string) => {
      if (def.type === 'number') {
        onChange(next ? Number(next) : undefined);
        return;
      }

      onChange(next);
    },
    [def.type, onChange],
  );

  const textInput = useBufferedInput(
    String(value ?? ''),
    `${nodeId}:${def.key}:${def.type}`,
    commitTextLikeValue,
  );

  const selectTouchedRef = useRef(false);
  const commitSelectIfNeeded = useCallback(
    (next: string) => {
      const current = value === undefined || value === null ? undefined : String(value);
      if (current === next) return;
      onChange(next);
    },
    [onChange, value],
  );

  switch (def.type) {
    case 'boolean':
      return (
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, color: 'var(--fc-text-secondary, #aaa)', cursor: 'pointer' }}>
          <input
            data-testid={`${fieldTestId}-input`}
            type="checkbox"
            checked={!!value}
            onChange={(e) => onChange(e.target.checked)}
            style={{ accentColor: colors.border }}
          />
          {def.label}
        </label>
      );

    case 'select': {
      const resolvedValue = value === undefined || value === null
        ? String(def.defaultValue ?? '')
        : String(value);

      return (
        <select
          data-testid={`${fieldTestId}-input`}
          value={resolvedValue}
          onFocus={() => { selectTouchedRef.current = true; }}
          onChange={(e) => {
            selectTouchedRef.current = true;
            commitSelectIfNeeded(e.target.value);
          }}
          onBlur={(e) => {
            if (selectTouchedRef.current) {
              commitSelectIfNeeded(e.currentTarget.value);
            }
            selectTouchedRef.current = false;
          }}
          style={{ ...inputStyle, cursor: 'pointer' }}
        >
          <option value="">-</option>
          {def.options?.map((opt) => (
            <option key={opt} value={opt}>{opt}</option>
          ))}
        </select>
      );
    }

    case 'number':
      return (
        <input
          data-testid={`${fieldTestId}-input`}
          type="number"
          value={textInput.value}
          placeholder={def.placeholder}
          onChange={(e) => textInput.onChange(e.target.value)}
          onFocus={textInput.onFocus}
          onBlur={textInput.onBlur}
          style={inputStyle}
        />
      );

    case 'textarea':
      return (
        <textarea
          data-testid={`${fieldTestId}-input`}
          value={textInput.value}
          placeholder={def.placeholder}
          onChange={(e) => textInput.onChange(e.target.value)}
          onFocus={textInput.onFocus}
          onBlur={textInput.onBlur}
          rows={3}
          style={{ ...inputStyle, resize: 'vertical' }}
        />
      );

    case 'code':
    case 'text':
    default:
      return (
        <input
          data-testid={`${fieldTestId}-input`}
          type="text"
          value={textInput.value}
          placeholder={def.placeholder}
          onChange={(e) => textInput.onChange(e.target.value)}
          onFocus={textInput.onFocus}
          onBlur={textInput.onBlur}
          style={inputStyle}
        />
      );
  }
}

const DATA_BLOCK_TYPES = new Set(['extract', 'parse', 'set', 'table', 'assert']);

function timeAgo(timestamp: number): string {
  const seconds = Math.floor((Date.now() - timestamp) / 1000);
  if (seconds < 5) return 'just now';
  if (seconds < 60) return `${seconds}s ago`;
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
  return `${Math.floor(seconds / 3600)}h ago`;
}

function TestDataBlockSection({
  selectedNodeId,
  blockType,
  props,
  colors,
}: {
  selectedNodeId: string;
  blockType: string;
  props: Record<string, unknown> | undefined;
  colors: { border: string; bg: string; badge: string };
}) {
  const variables = useFlowStore((s) => s.variables);
  const isRunning = useFlowStore((s) => s.isRunning);
  const testResult = useFlowStore((s) => s.dataBlockTestResults.get(selectedNodeId));
  const clearResult = useFlowStore((s) => s.clearDataBlockTestResult);
  const [isTesting, setIsTesting] = useState(false);
  const [, setTick] = useState(0);

  // Refresh the "Xs ago" label periodically
  useEffect(() => {
    if (!testResult) return;
    const id = setInterval(() => setTick((t) => t + 1), 5000);
    return () => clearInterval(id);
  }, [testResult]);

  // Reset testing spinner when result arrives
  useEffect(() => {
    if (testResult) setIsTesting(false);
  }, [testResult]);

  const hasVariables = variables.length > 0;
  const disabled = isRunning || !hasVariables || isTesting;

  const handleTest = () => {
    const varsObj: Record<string, unknown> = {};
    for (const v of variables) {
      varsObj[v.name] = v.value;
    }
    setIsTesting(true);
    messageBus.send({
      type: CANVAS_HOST_MESSAGES.outgoing.testDataBlock,
      stepId: selectedNodeId,
      blockType,
      props: props ?? {},
      variables: varsObj,
    });
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <div style={{ borderTop: '1px solid var(--fc-panel-border, #2a2a4a)', paddingTop: 10 }}>
        <button
          data-testid="test-data-block-btn"
          onClick={handleTest}
          disabled={disabled}
          title={
            isRunning ? 'Cannot test while flow is running'
              : !hasVariables ? 'Run the flow first to populate variables'
                : `Test this ${blockType} block against current variables`
          }
          style={{
            width: '100%',
            padding: '6px 10px',
            background: disabled ? '#333' : colors.badge,
            color: disabled ? '#666' : colors.border === '#9b59b6' ? '#fff' : '#000',
            border: 'none',
            borderRadius: 4,
            fontSize: 12,
            fontWeight: 600,
            cursor: disabled ? 'not-allowed' : 'pointer',
            opacity: disabled ? 0.5 : 1,
            transition: 'opacity 0.15s',
          }}
        >
          {isTesting ? 'Testing...' : 'Test Block'}
        </button>
      </div>

      {testResult && (
        <TestResultDisplay result={testResult} onDismiss={() => clearResult(selectedNodeId)} />
      )}
    </div>
  );
}

function TestResultDisplay({ result, onDismiss }: { result: DataBlockTestResult; onDismiss: () => void }) {
  const borderColor = result.success ? '#2ecc71' : '#e74c3c';
  const bgColor = result.success ? 'rgba(46, 204, 113, 0.08)' : 'rgba(231, 76, 60, 0.08)';

  return (
    <div style={{
      borderLeft: `3px solid ${borderColor}`,
      background: bgColor,
      borderRadius: '0 4px 4px 0',
      padding: '8px 10px',
      fontSize: 12,
      position: 'relative',
    }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 }}>
        <span style={{ fontWeight: 600, color: borderColor, fontSize: 11 }}>
          {result.success ? 'Success' : 'Failed'}
        </span>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span style={{ fontSize: 10, color: 'var(--fc-text-muted, #555)' }}>
            {timeAgo(result.timestamp)}
          </span>
          <button
            onClick={onDismiss}
            style={{
              background: 'none',
              border: 'none',
              color: 'var(--fc-text-muted, #555)',
              cursor: 'pointer',
              fontSize: 14,
              padding: 0,
              lineHeight: 1,
            }}
          >
            ×
          </button>
        </div>
      </div>

      {result.error && (
        <div style={{ color: '#e74c3c', fontFamily: 'monospace', fontSize: 11, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
          {result.error}
        </div>
      )}

      {result.output && (
        <div style={{
          fontFamily: 'monospace',
          fontSize: 11,
          color: 'var(--fc-text, #ccc)',
          whiteSpace: 'pre-wrap',
          wordBreak: 'break-word',
          maxHeight: 150,
          overflowY: 'auto',
          marginTop: result.error ? 4 : 0,
        }}>
          {result.output}
        </div>
      )}

      {result.changedKeys && result.changedKeys.length > 0 && (
        <div style={{ marginTop: 6, display: 'flex', flexWrap: 'wrap', gap: 4 }}>
          {result.changedKeys.filter((k) => k !== '_timestamp').map((key) => (
            <span key={key} style={{
              background: 'rgba(224, 192, 64, 0.15)',
              border: '1px solid rgba(224, 192, 64, 0.3)',
              borderRadius: 3,
              padding: '1px 5px',
              fontSize: 10,
              color: '#e0c040',
              fontFamily: 'monospace',
            }}>
              {key}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}

const START_BOOL_FIELDS: { key: string; label: string }[] = [
  { key: 'debug', label: 'Debug Mode' },
  { key: 'nobanner', label: 'No Banner' },
  { key: 'suppress_missing_column_warning', label: 'Suppress Missing Column Warning' },
  { key: 'library', label: 'Library (non-executable)' },
];

function StartProperties({
  nodeId,
  data,
  onPropChange,
  onLabelChange,
}: {
  nodeId: string;
  data: { label?: string; props?: Record<string, unknown> };
  onPropChange: (key: string, value: unknown) => void;
  onLabelChange: (label: string) => void;
}) {
  const props = data.props ?? {};

  const nameInput = useBufferedInput(
    String(props.name ?? ''),
    `${nodeId}:start-name`,
    (val) => {
      onPropChange('name', val || undefined);
      onLabelChange(val || 'Untitled Script');
    },
  );

  const descInput = useBufferedInput(
    String(props.description ?? ''),
    `${nodeId}:start-desc`,
    (val) => onPropChange('description', val || undefined),
  );

  const envInput = useBufferedInput(
    String(props.environment ?? ''),
    `${nodeId}:start-env`,
    (val) => onPropChange('environment', val || undefined),
  );

  const versionInput = useBufferedInput(
    String(props.version ?? ''),
    `${nodeId}:start-version`,
    (val) => onPropChange('version', val ? Number(val) : undefined),
  );

  const colors = { text: '#80d4a0', border: '#2ecc71', bg: '#0d2a1a' };

  const inputStyle: React.CSSProperties = {
    width: '100%',
    padding: '4px 6px',
    background: 'var(--fc-input-bg, #0d1117)',
    border: `1px solid ${colors.border}44`,
    borderRadius: 4,
    color: 'var(--fc-text, #ccc)',
    fontSize: 12,
    outline: 'none',
  };

  const varsCount = props.vars ? Object.keys(props.vars as Record<string, unknown>).length : 0;
  const importsCount = Array.isArray(props.imports) ? (props.imports as unknown[]).length : 0;

  return (
    <div
      data-testid="properties-panel"
      style={{
        flex: 1,
        overflowY: 'auto',
        padding: 12,
        display: 'flex',
        flexDirection: 'column',
        gap: 12,
      }}
    >
      {/* Header */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        gap: 6,
        paddingBottom: 8,
        borderBottom: '1px solid var(--fc-panel-border, #2a2a4a)',
      }}>
        <span style={{
          background: '#2ecc71',
          color: '#000',
          fontSize: 10,
          fontWeight: 700,
          padding: '2px 6px',
          borderRadius: 3,
          textTransform: 'uppercase',
        }}>
          START
        </span>
        <span style={{ color: 'var(--fc-text, #ccc)', fontSize: 12, fontWeight: 600 }}>
          Script Settings
        </span>
      </div>

      {/* Name */}
      <div>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)', display: 'block', marginBottom: 3 }}>
          Name
        </label>
        <input
          data-testid="start-name-input"
          type="text"
          value={nameInput.value}
          placeholder="Untitled Script"
          onChange={(e) => nameInput.onChange(e.target.value)}
          onFocus={nameInput.onFocus}
          onBlur={nameInput.onBlur}
          style={inputStyle}
        />
      </div>

      {/* Description */}
      <div>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)', display: 'block', marginBottom: 3 }}>
          Description
        </label>
        <textarea
          data-testid="start-description-input"
          value={descInput.value}
          placeholder="What does this script do?"
          onChange={(e) => descInput.onChange(e.target.value)}
          onFocus={descInput.onFocus}
          onBlur={descInput.onBlur}
          rows={2}
          style={{ ...inputStyle, resize: 'vertical' }}
        />
      </div>

      {/* Environment */}
      <div>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)', display: 'block', marginBottom: 3 }}>
          Environment
        </label>
        <input
          data-testid="start-environment-input"
          type="text"
          value={envInput.value}
          placeholder="Optional environment name"
          onChange={(e) => envInput.onChange(e.target.value)}
          onFocus={envInput.onFocus}
          onBlur={envInput.onBlur}
          style={inputStyle}
        />
      </div>

      {/* Version */}
      <div>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)', display: 'block', marginBottom: 3 }}>
          Version
        </label>
        <input
          data-testid="start-version-input"
          type="number"
          value={versionInput.value}
          placeholder="1"
          onChange={(e) => versionInput.onChange(e.target.value)}
          onFocus={versionInput.onFocus}
          onBlur={versionInput.onBlur}
          style={inputStyle}
        />
      </div>

      {/* Boolean flags */}
      <div style={{
        borderTop: '1px solid var(--fc-panel-border, #2a2a4a)',
        paddingTop: 10,
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
      }}>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)' }}>Flags</label>
        {START_BOOL_FIELDS.map((field) => (
          <label
            key={field.key}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              fontSize: 12,
              color: 'var(--fc-text-secondary, #aaa)',
              cursor: 'pointer',
            }}
          >
            <input
              data-testid={`start-${field.key}-input`}
              type="checkbox"
              checked={!!props[field.key]}
              onChange={(e) => onPropChange(field.key, e.target.checked)}
              style={{ accentColor: '#2ecc71' }}
            />
            {field.label}
          </label>
        ))}
      </div>

      {/* Read-only summaries for vars and imports */}
      {(varsCount > 0 || importsCount > 0) && (
        <div style={{
          borderTop: '1px solid var(--fc-panel-border, #2a2a4a)',
          paddingTop: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 4,
        }}>
          {varsCount > 0 && (
            <div style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)' }}>
              {varsCount} variable{varsCount !== 1 ? 's' : ''} defined
            </div>
          )}
          {importsCount > 0 && (
            <div style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)' }}>
              {importsCount} import{importsCount !== 1 ? 's' : ''}
            </div>
          )}
        </div>
      )}

      {/* Footer */}
      <div style={{
        marginTop: 'auto',
        paddingTop: 12,
        borderTop: '1px solid var(--fc-panel-border, #2a2a4a)',
        fontSize: 11,
        color: 'var(--fc-text-muted, #555)',
        lineHeight: 1.5,
      }}>
        Script-level settings that control execution behavior. These appear in the YAML preamble above the steps.
      </div>
    </div>
  );
}

export default function Properties() {
  const selectedNodeIds = useFlowStore((s) => s.selectedNodeIds);
  const nodes = useFlowStore((s) => s.nodes);
  const pushSnapshot = useFlowStore((s) => s.pushSnapshot);
  const updateNodeLabel = useFlowStore((s) => s.updateNodeLabel);
  const updateNodeProp = useFlowStore((s) => s.updateNodeProp);

  const selectedNodeId = selectedNodeIds.size === 1 ? [...selectedNodeIds][0] : null;
  const node = selectedNodeId ? nodes.find((candidate) => candidate.id === selectedNodeId) ?? null : null;
  const blockData = node?.data as BlockNodeData | undefined;
  const def = blockData?.blockType ? blockDefMap.get(blockData.blockType) : null;

  const snapshotTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (snapshotTimeoutRef.current) {
        clearTimeout(snapshotTimeoutRef.current);
        snapshotTimeoutRef.current = null;
      }
    };
  }, []);

  const pushDebouncedSnapshot = useCallback(() => {
    if (snapshotTimeoutRef.current) return;
    pushSnapshot('Edit property');
    snapshotTimeoutRef.current = setTimeout(() => {
      snapshotTimeoutRef.current = null;
    }, 500);
  }, [pushSnapshot]);

  const updateProp = useCallback(
    (key: string, value: unknown) => {
      if (!selectedNodeId) return;
      pushDebouncedSnapshot();
      updateNodeProp(selectedNodeId, key, value);
    },
    [selectedNodeId, pushDebouncedSnapshot, updateNodeProp],
  );

  const updateLabel = useCallback(
    (label: string) => {
      if (!selectedNodeId) return;
      pushDebouncedSnapshot();
      updateNodeLabel(selectedNodeId, label);
    },
    [selectedNodeId, pushDebouncedSnapshot, updateNodeLabel],
  );

  if (selectedNodeIds.size > 1) {
    return (
      <div
        data-testid="properties-panel"
        style={{
          flex: 1,
          padding: 16,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <span style={{ color: 'var(--fc-text-muted, #555)', fontSize: 12, textAlign: 'center' }}>
          {selectedNodeIds.size} blocks selected
        </span>
      </div>
    );
  }

  // Start node: render custom script-settings form (before def check,
  // since _start is not in the block registry and def will be null)
  if (selectedNodeId && node && blockData?.blockType === '_start') {
    return (
      <StartProperties
        nodeId={selectedNodeId}
        data={blockData}
        onPropChange={updateProp}
        onLabelChange={updateLabel}
      />
    );
  }

  if (!node || !def || !blockData || !selectedNodeId) {
    return (
      <div
        data-testid="properties-panel"
        style={{
          flex: 1,
          padding: 16,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <span style={{ color: 'var(--fc-text-muted, #555)', fontSize: 12, textAlign: 'center' }}>
          Select a block to edit its properties
        </span>
      </div>
    );
  }

  const colors = categoryColors[def.category as BlockCategory];
  const isChild = !!(blockData.props?.['_isChildOf']);
  const branchLabel = blockData.props?.['_branchLabel'] as string | undefined;
  const branchColor = blockData.props?.['_branchColor'] as string | undefined;

  if (isChild) {
    return (
      <div
        data-testid="properties-panel"
        style={{
          flex: 1,
          overflowY: 'auto',
          padding: 12,
          display: 'flex',
          flexDirection: 'column',
          gap: 12,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, paddingBottom: 8, borderBottom: '1px solid var(--fc-panel-border, #2a2a4a)' }}>
          <span style={{
            background: colors.badge,
            color: colors.badgeText,
            fontSize: 10,
            fontWeight: 700,
            padding: '2px 6px',
            borderRadius: 3,
            textTransform: 'uppercase',
          }}>
            {def.type}
          </span>
          <span style={{ color: 'var(--fc-text, #ccc)', fontSize: 12, fontWeight: 600 }}>
            {def.label}
          </span>
        </div>

        {branchLabel && (
          <div style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            padding: '4px 8px',
            background: `${branchColor || '#555'}15`,
            borderRadius: 4,
            borderLeft: `2px solid ${branchColor || '#555'}`,
          }}>
            <span style={{ fontSize: 10, color: branchColor || '#888', fontWeight: 600, textTransform: 'uppercase' }}>
              {branchLabel}
            </span>
            <span style={{ fontSize: 10, color: 'var(--fc-text-muted, #666)' }}>branch (read-only)</span>
          </div>
        )}

        {def.properties.map((propDef) => {
          const val = blockData.props?.[propDef.key];
          if (val === undefined || val === null || val === '') return null;
          return (
            <div key={`${selectedNodeId}-${propDef.key}`}>
              <label style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)', display: 'block', marginBottom: 3 }}>
                {propDef.label}
              </label>
              <div style={{
                padding: '4px 6px',
                background: 'var(--fc-input-bg, #0d1117)99',
                borderRadius: 4,
                color: 'var(--fc-text-secondary, #aaa)',
                fontSize: 12,
                fontFamily: propDef.type === 'code' ? 'monospace' : 'inherit',
                whiteSpace: 'pre-wrap',
                wordBreak: 'break-all',
              }}>
                {String(val)}
              </div>
            </div>
          );
        })}

        <div style={{
          marginTop: 'auto',
          paddingTop: 12,
          borderTop: '1px solid var(--fc-panel-border, #2a2a4a)',
          fontSize: 11,
          color: 'var(--fc-text-muted, #555)',
          lineHeight: 1.5,
        }}>
          {def.description}
        </div>
      </div>
    );
  }

  return (
    <div
      data-testid="properties-panel"
      style={{
        flex: 1,
        overflowY: 'auto',
        padding: 12,
        display: 'flex',
        flexDirection: 'column',
        gap: 12,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, paddingBottom: 8, borderBottom: '1px solid var(--fc-panel-border, #2a2a4a)' }}>
        <span style={{
          background: colors.badge,
          color: colors.badgeText,
          fontSize: 10,
          fontWeight: 700,
          padding: '2px 6px',
          borderRadius: 3,
          textTransform: 'uppercase',
        }}>
          {def.type}
        </span>
        <span style={{ color: 'var(--fc-text, #ccc)', fontSize: 12, fontWeight: 600 }}>
          {def.label}
        </span>
      </div>

      {def.properties.map((propDef) => {
        const fieldTestId = `properties-field-${propDef.key}-${propDef.type}`;
        return (
          <div key={`${selectedNodeId}-${propDef.key}`} data-testid={fieldTestId}>
            {propDef.type !== 'boolean' && (
              <label style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)', display: 'block', marginBottom: 3 }}>
                {propDef.label}
                {propDef.required && <span style={{ color: '#e74c3c', marginLeft: 2 }}>*</span>}
              </label>
            )}
            <PropertyField
              def={propDef}
              value={blockData.props?.[propDef.key]}
              onChange={(val) => updateProp(propDef.key, val)}
              colors={colors}
              fieldTestId={fieldTestId}
              nodeId={selectedNodeId}
            />
          </div>
        );
      })}

      {DATA_BLOCK_TYPES.has(blockData.blockType) && (
        <TestDataBlockSection
          selectedNodeId={selectedNodeId}
          blockType={blockData.blockType}
          props={blockData.props}
          colors={colors}
        />
      )}

      <div style={{
        marginTop: 'auto',
        paddingTop: 12,
        borderTop: '1px solid var(--fc-panel-border, #2a2a4a)',
        fontSize: 11,
        color: 'var(--fc-text-muted, #555)',
        lineHeight: 1.5,
      }}>
        {def.description}
      </div>
    </div>
  );
}

import { useCallback, useEffect, useRef, useState } from 'react';
import { blockDefMap, categoryColors, type BlockCategory, type PropertyDef } from '../blockDefs/registry';
import type { BlockNodeData } from '../nodes/BaseBlock';
import { useFlowStore } from '../stores/useFlowStore';

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

  const displayNameInput = useBufferedInput(
    String(blockData?.label ?? ''),
    `${selectedNodeId ?? 'none'}:display-name`,
    updateLabel,
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

      <div>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)', display: 'block', marginBottom: 3 }}>Display Name</label>
        <input
          data-testid="properties-display-name-input"
          type="text"
          value={displayNameInput.value}
          placeholder={def.label}
          onChange={(e) => displayNameInput.onChange(e.target.value)}
          onFocus={displayNameInput.onFocus}
          onBlur={displayNameInput.onBlur}
          style={{
            width: '100%',
            padding: '4px 6px',
            background: 'var(--fc-input-bg, #0d1117)',
            border: `1px solid ${colors.border}44`,
            borderRadius: 4,
            color: 'var(--fc-text, #ccc)',
            fontSize: 12,
            outline: 'none',
          }}
        />
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

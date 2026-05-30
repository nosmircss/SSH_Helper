import { useCallback, useEffect, useRef, useState } from 'react';
import { blockDefMap, categoryColors, type BlockCategory, type PropertyDef } from '../blockDefs/registry';
import type { BlockNodeData } from '../nodes/BaseBlock';
import { useFlowStore } from '../stores/useFlowStore';
import { messageBus } from '../MessageBus';
import { CANVAS_HOST_MESSAGES } from '../communication-message-types';
import type { DataBlockTestResult } from '../stores/slices/executionSlice';
import { mix } from '../utils/tokens';
import { branchColorVar } from '../utils/branchBands';

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

function createBrowseRequestId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  return `browse-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

type ChoiceEditorMode = 'source' | 'static';

interface ChoiceOptionRow {
  label: string;
  value: string;
}

interface ChoiceEditorState {
  mode: ChoiceEditorMode;
  source: string;
  rows: ChoiceOptionRow[];
}

const SIMPLE_VARIABLE_NAME_REGEX = /^[A-Za-z_]\w*$/;
const WHOLE_TOKEN_VARIABLE_REGEX = /^\$\{[^{}]+\}$/;
const WHOLE_TOKEN_HANDLEBARS_VARIABLE_REGEX = /^\{\{[^{}]+\}\}$/;

function normalizeChoiceOptionRow(row: ChoiceOptionRow): ChoiceOptionRow | null {
  const label = row.label.trim();
  const value = row.value.trim();
  if (!label && !value) return null;
  if (label && value) return { label, value };
  return label
    ? { label, value: label }
    : { label: value, value };
}

function parseDelimitedChoiceRows(raw: string): ChoiceOptionRow[] {
  return raw
    .split(/[,\r\n]+/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0)
    .map((item) => ({ label: item, value: item }));
}

function toChoiceOptionRow(value: unknown): ChoiceOptionRow | null {
  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (!trimmed) return null;
    return { label: trimmed, value: trimmed };
  }

  if (!value || typeof value !== 'object') return null;
  const row = value as Record<string, unknown>;
  const rawLabel = typeof row.label === 'string' ? row.label : '';
  const rawValue = typeof row.value === 'string' ? row.value : '';
  return normalizeChoiceOptionRow({
    label: rawLabel,
    value: rawValue,
  });
}

function parseJsonChoiceRows(raw: string): ChoiceOptionRow[] {
  try {
    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed
      .map(toChoiceOptionRow)
      .filter((row): row is ChoiceOptionRow => row !== null);
  } catch {
    return [];
  }
}

function inferChoiceEditorState(value: unknown): ChoiceEditorState {
  if (Array.isArray(value)) {
    const rows = value
      .map(toChoiceOptionRow)
      .filter((row): row is ChoiceOptionRow => row !== null);
    return {
      mode: 'static',
      source: '',
      rows: rows.length > 0 ? rows : [{ label: '', value: '' }],
    };
  }

  const scalar = typeof value === 'string' ? value : '';
  const trimmed = scalar.trim();
  if (!trimmed) {
    return { mode: 'source', source: '', rows: [{ label: '', value: '' }] };
  }

  if (trimmed.startsWith('[') && trimmed.endsWith(']')) {
    const jsonRows = parseJsonChoiceRows(trimmed);
    if (jsonRows.length > 0) {
      return {
        mode: 'static',
        source: '',
        rows: jsonRows,
      };
    }
  }

  if (trimmed.includes(',') || trimmed.includes('\n') || trimmed.includes('\r')) {
    const rows = parseDelimitedChoiceRows(trimmed);
    return {
      mode: 'static',
      source: '',
      rows: rows.length > 0 ? rows : [{ label: '', value: '' }],
    };
  }

  return {
    mode: 'source',
    source: scalar,
    rows: [{ label: '', value: '' }],
  };
}

function serializeStaticChoiceRows(rows: ChoiceOptionRow[]): unknown[] {
  const normalized = rows
    .map(normalizeChoiceOptionRow)
    .filter((row): row is ChoiceOptionRow => row !== null);

  return normalized.map((row) => {
    if (row.label === row.value) {
      return row.value;
    }

    return {
      label: row.label,
      value: row.value,
    };
  });
}

function isValidChoiceSourceToken(value: string): boolean {
  const trimmed = value.trim();
  if (!trimmed) return false;
  return SIMPLE_VARIABLE_NAME_REGEX.test(trimmed)
    || WHOLE_TOKEN_VARIABLE_REGEX.test(trimmed)
    || WHOLE_TOKEN_HANDLEBARS_VARIABLE_REGEX.test(trimmed);
}

function buildTokenInsertion(current: string, variableName: string): string {
  const token = `\${${variableName}}`;
  const trimmed = current.trim();
  if (!trimmed || trimmed === '${var}' || trimmed === '${') {
    return token;
  }

  if (trimmed === '{{var}}' || trimmed === '{{') {
    return `{{${variableName}}}`;
  }

  const separator = current.endsWith(' ') || current.length === 0 ? '' : ' ';
  return `${current}${separator}${token}`;
}

function ChoiceOptionsEditor({
  value,
  onChange,
  fieldTestId,
  colors,
  required,
}: {
  value: unknown;
  onChange: (val: unknown) => void;
  fieldTestId: string;
  colors: { border: string };
  required: boolean;
}) {
  const variables = useFlowStore((s) => s.variables);
  const variableNames = variables
    .map((entry) => entry.name)
    .filter((name) => name.trim().length > 0);
  const state = inferChoiceEditorState(value);
  const [sourceInsertChoice, setSourceInsertChoice] = useState('');

  useEffect(() => {
    setSourceInsertChoice('');
  }, [fieldTestId, state.mode]);

  const sourceInput = useBufferedInput(
    state.mode === 'source' ? state.source : '',
    `${fieldTestId}:source`,
    (next) => onChange(next),
  );

  const setMode = useCallback((mode: ChoiceEditorMode) => {
    if (mode === state.mode) return;

    if (mode === 'source') {
      onChange('${var}');
      return;
    }

    onChange([{ label: '', value: '' }]);
  }, [onChange, state.mode]);

  const commitRows = useCallback((nextRows: ChoiceOptionRow[]) => {
    onChange(serializeStaticChoiceRows(nextRows));
  }, [onChange]);

  const updateRow = useCallback((index: number, key: keyof ChoiceOptionRow, nextValue: string) => {
    const nextRows = state.rows.map((row, rowIndex) => (
      rowIndex === index
        ? { ...row, [key]: nextValue }
        : row
    ));
    commitRows(nextRows);
  }, [commitRows, state.rows]);

  const removeRow = useCallback((index: number) => {
    const nextRows = state.rows.filter((_, rowIndex) => rowIndex !== index);
    commitRows(nextRows.length > 0 ? nextRows : [{ label: '', value: '' }]);
  }, [commitRows, state.rows]);

  const addRow = useCallback(() => {
    const nextIndex = state.rows.length + 1;
    commitRows([
      ...state.rows,
      {
        label: `Option ${nextIndex}`,
        value: `option_${nextIndex}`,
      },
    ]);
  }, [commitRows, state.rows]);

  const moveRow = useCallback((index: number, direction: -1 | 1) => {
    const target = index + direction;
    if (target < 0 || target >= state.rows.length) return;
    const nextRows = [...state.rows];
    const tmp = nextRows[target];
    nextRows[target] = nextRows[index];
    nextRows[index] = tmp;
    commitRows(nextRows);
  }, [commitRows, state.rows]);

  const staticOptions = serializeStaticChoiceRows(state.rows);
  const sourceError = state.mode === 'source'
    ? (!sourceInput.value.trim()
      ? (required ? 'Options source is required.' : null)
      : (!isValidChoiceSourceToken(sourceInput.value) ? 'Use var_name, ${var}, or {{var}}.' : null))
    : null;
  const staticError = state.mode === 'static' && required && staticOptions.length === 0
    ? 'Add at least one option.'
    : null;
  const error = sourceError ?? staticError;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <div style={{ display: 'flex', gap: 6 }}>
        <button
          data-testid={`${fieldTestId}-mode-source`}
          type="button"
          data-active={state.mode === 'source' ? 'true' : 'false'}
          onClick={() => setMode('source')}
          style={{
            padding: '3px 8px',
            borderRadius: 4,
            border: `1px solid ${state.mode === 'source' ? colors.border : 'var(--fc-border)'}`,
            background: state.mode === 'source' ? 'var(--fc-accent-surface)' : 'var(--fc-surface-2)',
            color: state.mode === 'source' ? 'var(--fc-accent-text)' : 'var(--fc-text-secondary)',
            fontSize: 11,
            cursor: 'pointer',
          }}
        >
          From Variable
        </button>
        <button
          data-testid={`${fieldTestId}-mode-static`}
          type="button"
          data-active={state.mode === 'static' ? 'true' : 'false'}
          onClick={() => setMode('static')}
          style={{
            padding: '3px 8px',
            borderRadius: 4,
            border: `1px solid ${state.mode === 'static' ? colors.border : 'var(--fc-border)'}`,
            background: state.mode === 'static' ? 'var(--fc-accent-surface)' : 'var(--fc-surface-2)',
            color: state.mode === 'static' ? 'var(--fc-accent-text)' : 'var(--fc-text-secondary)',
            fontSize: 11,
            cursor: 'pointer',
          }}
        >
          Static Options
        </button>
      </div>

      {state.mode === 'source' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <input
            data-testid={`${fieldTestId}-source-input`}
            type="text"
            value={sourceInput.value}
            placeholder="${var}"
            onChange={(e) => sourceInput.onChange(e.target.value)}
            onFocus={sourceInput.onFocus}
            onBlur={sourceInput.onBlur}
            style={{
              width: '100%',
              padding: '4px 6px',
              background: 'var(--fc-input-bg)',
              border: `1px solid ${error ? 'var(--fc-state-error)' : mix(colors.border, 27)}`,
              borderRadius: 4,
              color: 'var(--fc-text)',
              fontSize: 12,
              outline: 'none',
              fontFamily: 'monospace',
            }}
          />
          {variableNames.length > 0 && (
            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
              <select
                data-testid={`${fieldTestId}-source-insert-var`}
                value={sourceInsertChoice}
                onChange={(e) => {
                  const variableName = e.target.value;
                  setSourceInsertChoice('');
                  if (!variableName) return;
                  sourceInput.onChange(buildTokenInsertion(sourceInput.value, variableName));
                }}
                style={{
                  minWidth: 160,
                  padding: '2px 6px',
                  background: 'var(--fc-input-bg)',
                  border: '1px solid var(--fc-border)',
                  borderRadius: 4,
                  color: 'var(--fc-text-secondary)',
                  fontSize: 11,
                  outline: 'none',
                }}
              >
                <option value="">Insert variable...</option>
                {variableNames.map((name) => (
                  <option key={name} value={name}>{name}</option>
                ))}
              </select>
            </div>
          )}
        </div>
      )}

      {state.mode === 'static' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {state.rows.map((row, index) => (
            <div
              key={`${fieldTestId}-row-${index}`}
              data-testid={`${fieldTestId}-row-${index}`}
              style={{ display: 'grid', gridTemplateColumns: '1fr 1fr auto', gap: 6, alignItems: 'center' }}
            >
              <input
                data-testid={`${fieldTestId}-row-${index}-label`}
                type="text"
                value={row.label}
                placeholder="Label"
                onChange={(e) => updateRow(index, 'label', e.target.value)}
                style={{
                  width: '100%',
                  padding: '4px 6px',
                  background: 'var(--fc-input-bg)',
                  border: `1px solid ${error ? 'var(--fc-state-error)' : mix(colors.border, 27)}`,
                  borderRadius: 4,
                  color: 'var(--fc-text)',
                  fontSize: 12,
                  outline: 'none',
                }}
              />
              <input
                data-testid={`${fieldTestId}-row-${index}-value`}
                type="text"
                value={row.value}
                placeholder="Value"
                onChange={(e) => updateRow(index, 'value', e.target.value)}
                style={{
                  width: '100%',
                  padding: '4px 6px',
                  background: 'var(--fc-input-bg)',
                  border: `1px solid ${error ? 'var(--fc-state-error)' : mix(colors.border, 27)}`,
                  borderRadius: 4,
                  color: 'var(--fc-text)',
                  fontSize: 12,
                  outline: 'none',
                }}
              />
              <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                <button
                  data-testid={`${fieldTestId}-row-${index}-up`}
                  type="button"
                  onClick={() => moveRow(index, -1)}
                  disabled={index === 0}
                  style={{ padding: '3px 6px', fontSize: 11, cursor: index === 0 ? 'default' : 'pointer' }}
                  title="Move up"
                >
                  Up
                </button>
                <button
                  data-testid={`${fieldTestId}-row-${index}-down`}
                  type="button"
                  onClick={() => moveRow(index, 1)}
                  disabled={index === state.rows.length - 1}
                  style={{ padding: '3px 6px', fontSize: 11, cursor: index === state.rows.length - 1 ? 'default' : 'pointer' }}
                  title="Move down"
                >
                  Down
                </button>
                <button
                  data-testid={`${fieldTestId}-row-${index}-remove`}
                  type="button"
                  onClick={() => removeRow(index)}
                  style={{ padding: '3px 6px', fontSize: 11, cursor: 'pointer' }}
                  title="Remove row"
                >
                  Remove
                </button>
              </div>
            </div>
          ))}
          <button
            data-testid={`${fieldTestId}-add-row`}
            type="button"
            onClick={addRow}
            style={{
              alignSelf: 'flex-start',
              padding: '4px 8px',
              border: `1px solid ${mix(colors.border, 40)}`,
              borderRadius: 4,
              background: 'var(--fc-button-bg)',
              color: 'var(--fc-text)',
              fontSize: 11,
              cursor: 'pointer',
            }}
          >
            + Add Option
          </button>
        </div>
      )}

      {error && (
        <div
          data-testid={`${fieldTestId}-error`}
          style={{ color: 'var(--fc-state-error)', fontSize: 11 }}
        >
          {error}
        </div>
      )}
    </div>
  );
}

function PropertyField({
  def,
  value,
  onChange,
  colors,
  fieldTestId,
  nodeId,
  blockType,
  required,
  invalid,
}: {
  def: PropertyDef;
  value: unknown;
  onChange: (val: unknown) => void;
  colors: { text: string; border: string; bg: string };
  fieldTestId: string;
  nodeId: string;
  blockType: string;
  required: boolean;
  invalid: boolean;
}) {
  const variables = useFlowStore((s) => s.variables);
  const variableNames = variables
    .map((entry) => entry.name)
    .filter((name) => name.trim().length > 0);
  const [insertChoice, setInsertChoice] = useState('');

  useEffect(() => {
    setInsertChoice('');
  }, [nodeId, def.key]);

  const inputStyle: React.CSSProperties = {
    width: '100%',
    padding: '4px 6px',
    background: 'var(--fc-input-bg)',
    border: `1px solid ${invalid ? 'var(--fc-state-error)' : mix(colors.border, 27)}`,
    borderRadius: 4,
    color: 'var(--fc-text)',
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
  const pendingBrowseRequestIdRef = useRef<string | null>(null);

  useEffect(() => {
    if (def.browse !== 'file') return;

    return messageBus.on(CANVAS_HOST_MESSAGES.incoming.browsePathResult, (msg) => {
      const requestId = typeof msg.requestId === 'string' ? msg.requestId : '';
      if (!requestId || pendingBrowseRequestIdRef.current !== requestId) return;

      pendingBrowseRequestIdRef.current = null;

      if (msg.canceled === true) return;
      const selectedPath = typeof msg.path === 'string' ? msg.path : '';
      if (!selectedPath) return;

      textInput.onChange(selectedPath);
    });
  }, [def.browse, textInput]);

  const requestPathBrowse = useCallback(() => {
    if (def.browse !== 'file') return;

    const requestId = createBrowseRequestId();
    pendingBrowseRequestIdRef.current = requestId;

    messageBus.send({
      type: CANVAS_HOST_MESSAGES.outgoing.browsePath,
      requestId,
      nodeId,
      propertyKey: def.key,
      currentPath: textInput.value,
      title: `Select ${def.label}`,
    });
  }, [def.browse, def.key, def.label, nodeId, textInput.value]);

  const selectTouchedRef = useRef(false);
  const commitSelectIfNeeded = useCallback(
    (next: string) => {
      const current = value === undefined || value === null ? undefined : String(value);
      if (current === next) return;
      onChange(next);
    },
    [onChange, value],
  );

  const renderInsertVariable = (onInsert: (variableName: string) => void) => {
    if (variableNames.length === 0) return null;
    return (
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 6 }}>
        <select
          data-testid={`${fieldTestId}-insert-var`}
          value={insertChoice}
          onChange={(e) => {
            const variableName = e.target.value;
            setInsertChoice('');
            if (!variableName) return;
            onInsert(variableName);
          }}
          style={{
            minWidth: 160,
            padding: '2px 6px',
            background: 'var(--fc-input-bg)',
            border: '1px solid var(--fc-border)',
            borderRadius: 4,
            color: 'var(--fc-text-secondary)',
            fontSize: 11,
            outline: 'none',
          }}
        >
          <option value="">Insert variable...</option>
          {variableNames.map((name) => (
            <option key={name} value={name}>{name}</option>
          ))}
        </select>
      </div>
    );
  };

  if (def.editor === 'choice-options' && (blockType === 'choose' || blockType === 'multiselect')) {
    return (
      <ChoiceOptionsEditor
        value={value}
        onChange={onChange}
        fieldTestId={fieldTestId}
        colors={{ border: colors.border }}
        required={required}
      />
    );
  }

  switch (def.type) {
    case 'boolean':
      return (
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, color: 'var(--fc-text-secondary)', cursor: 'pointer' }}>
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
      const placeholderText = `Select ${def.label.toLowerCase()}...`;

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
          <option value="" disabled={required}>{placeholderText}</option>
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
        <div>
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
          {renderInsertVariable((variableName) => {
            textInput.onChange(buildTokenInsertion(textInput.value, variableName));
          })}
        </div>
      );

    case 'text':
      if (def.browse === 'file') {
        return (
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <input
                data-testid={`${fieldTestId}-input`}
                type="text"
                value={textInput.value}
                placeholder={def.placeholder}
                onChange={(e) => textInput.onChange(e.target.value)}
                onFocus={textInput.onFocus}
                onBlur={textInput.onBlur}
                style={{ ...inputStyle, flex: 1, minWidth: 0 }}
              />
              <button
                data-testid={`${fieldTestId}-browse`}
                type="button"
                onClick={requestPathBrowse}
                style={{
                  padding: '4px 8px',
                  background: 'var(--fc-button-bg)',
                  border: `1px solid ${mix(colors.border, 40)}`,
                  borderRadius: 4,
                  color: 'var(--fc-text)',
                  fontSize: 11,
                  cursor: 'pointer',
                  whiteSpace: 'nowrap',
                }}
              >
                Browse...
              </button>
            </div>
            {renderInsertVariable((variableName) => {
              textInput.onChange(buildTokenInsertion(textInput.value, variableName));
            })}
          </div>
        );
      }

      return (
        <div>
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
          {renderInsertVariable((variableName) => {
            textInput.onChange(buildTokenInsertion(textInput.value, variableName));
          })}
        </div>
      );

    case 'code':
    default:
      return (
        <div>
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
          {renderInsertVariable((variableName) => {
            textInput.onChange(buildTokenInsertion(textInput.value, variableName));
          })}
        </div>
      );
  }
}

const DATA_BLOCK_TYPES = new Set(['extract', 'parse', 'set', 'table', 'assert']);

function toBoolean(value: unknown, defaultValue: boolean): boolean {
  if (typeof value === 'boolean') return value;
  if (typeof value === 'number') return value !== 0;
  if (typeof value === 'string') {
    const normalized = value.trim().toLowerCase();
    if (normalized === 'true' || normalized === 'yes' || normalized === '1') return true;
    if (normalized === 'false' || normalized === 'no' || normalized === '0') return false;
  }

  return defaultValue;
}

function hasAnyValue(value: unknown): boolean {
  if (value === null || value === undefined) return false;
  if (typeof value === 'string') return value.trim().length > 0;
  if (typeof value === 'number') return !Number.isNaN(value);
  if (Array.isArray(value)) return value.length > 0;
  return true;
}

function hasExplicitProperty(props: Record<string, unknown> | undefined, key: string): boolean {
  return !!props && Object.prototype.hasOwnProperty.call(props, key);
}

function resolveDisplayedPropertyValue(
  blockType: string,
  propDef: PropertyDef,
  props: Record<string, unknown> | undefined,
): unknown {
  const rawValue = props?.[propDef.key];

  if (blockType === 'readfile' && propDef.key === 'autobrowse' && !hasExplicitProperty(props, 'autobrowse')) {
    const selectFile = toBoolean(props?.select_file, false);
    const pathOnly = toBoolean(props?.path_only, false);
    if (selectFile && pathOnly) {
      return true;
    }
  }

  return rawValue;
}

function isDynamicRuntimeValue(value: string): boolean {
  return value.includes('${') && value.includes('}');
}

function isPropertyRequired(
  blockType: string,
  propDef: PropertyDef,
  props: Record<string, unknown> | undefined,
): boolean {
  const staticRequired = !!propDef.required;

  if (blockType === 'readfile' && propDef.key === 'path') {
    const selectFile = toBoolean(props?.select_file, false);
    return !selectFile;
  }

  if (blockType === 'readfile' && propDef.key === 'into') {
    const pathOnly = toBoolean(props?.path_only, false);
    return !pathOnly;
  }

  if (blockType === 'readfile' && propDef.key === 'path_into') {
    const pathOnly = toBoolean(props?.path_only, false);
    return pathOnly;
  }

  if (blockType === 'http') {
    const auth = String(props?.auth ?? 'none').trim().toLowerCase();
    if (isDynamicRuntimeValue(auth)) return staticRequired;

    if (propDef.key === 'username' || propDef.key === 'password') {
      return auth === 'basic';
    }

    if (propDef.key === 'token') {
      return auth === 'bearer';
    }

    return staticRequired;
  }

  if (blockType === 'interactive') {
    const showWindow = toBoolean(props?.show_window, true);
    if (showWindow) return staticRequired;

    if (propDef.key === 'command') {
      return true;
    }

    if (propDef.key === 'max_seconds' || propDef.key === 'max_lines') {
      const hasMaxSeconds = hasAnyValue(props?.max_seconds);
      const hasMaxLines = hasAnyValue(props?.max_lines);
      return !hasMaxSeconds && !hasMaxLines;
    }
  }

  return staticRequired;
}

type PropertyPaneGroup = 'core' | 'advanced' | 'on_error';

const ADVANCED_PROPERTY_KEYS = new Set([
  'default',
  'validate',
  'validation_error',
  'font_size',
  'min',
  'max',
  'timeout',
  'retry',
  'retry_delay',
  'fail_on_nonzero',
  'suppress',
  'expect',
  'capture',
  'headers',
  'follow_redirects',
  'allow_failure',
  'verify_tls',
  'auth',
  'username',
  'password',
  'token',
  'content_type',
  'encoding',
  'max_lines',
  'trim_lines',
  'skip_empty_lines',
  'pretty',
  'format',
  'volume',
  'wait',
]);

function resolvePropertyGroup(propDef: PropertyDef): PropertyPaneGroup {
  if (propDef.group) return propDef.group;
  if (propDef.key === 'on_error') return 'on_error';
  if (ADVANCED_PROPERTY_KEYS.has(propDef.key)) return 'advanced';
  return 'core';
}

function getChoiceStaticOptionValues(value: unknown): Set<string> {
  const editorState = inferChoiceEditorState(value);
  if (editorState.mode !== 'static') return new Set<string>();

  const values = new Set<string>();
  const normalizedRows = serializeStaticChoiceRows(editorState.rows);
  for (const option of normalizedRows) {
    if (typeof option === 'string') {
      const trimmed = option.trim();
      if (trimmed.length > 0) values.add(trimmed);
      continue;
    }

    if (!option || typeof option !== 'object') continue;
    const optionRecord = option as Record<string, unknown>;
    const valueText = typeof optionRecord.value === 'string'
      ? optionRecord.value
      : '';
    const trimmed = valueText.trim();
    if (trimmed.length > 0) values.add(trimmed);
  }

  return values;
}

function getPropertyValidationMessage(
  blockType: string,
  propDef: PropertyDef,
  value: unknown,
  required: boolean,
): string | null {
  if (propDef.editor === 'choice-options' && (blockType === 'choose' || blockType === 'multiselect')) {
    const editorState = inferChoiceEditorState(value);
    if (editorState.mode === 'source') {
      const source = editorState.source.trim();
      if (required && source.length === 0) return 'Options source is required.';
      if (source.length > 0 && !isValidChoiceSourceToken(source)) return 'Use var_name, ${var}, or {{var}}.';
      return null;
    }

    if (required) {
      const staticOptions = serializeStaticChoiceRows(editorState.rows);
      if (staticOptions.length === 0) return 'Add at least one option.';
    }

    return null;
  }

  if (!required) return null;

  if (propDef.type === 'select') {
    const resolved = value === undefined || value === null
      ? propDef.defaultValue
      : value;
    return hasAnyValue(resolved) ? null : 'Please choose an option.';
  }

  return hasAnyValue(value) ? null : `${propDef.label} is required.`;
}

function getChooseDefaultWarning(defaultValue: unknown, optionsValue: unknown): string | null {
  const defaultText = typeof defaultValue === 'string' ? defaultValue.trim() : '';
  if (defaultText.length === 0) return null;

  const optionState = inferChoiceEditorState(optionsValue);
  if (optionState.mode !== 'static') return null;

  const allowedValues = getChoiceStaticOptionValues(optionsValue);
  return allowedValues.has(defaultText)
    ? null
    : 'Default value is not in the static options list.';
}

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
      <div style={{ borderTop: '1px solid var(--fc-panel-border)', paddingTop: 10 }}>
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
            background: disabled ? 'var(--fc-surface-disabled)' : colors.badge,
            color: disabled ? 'var(--fc-text-faint)' : colors.border === 'var(--fc-cat-data-border)' ? 'var(--fc-border-selected)' : 'var(--fc-on-accent)',
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
  const borderColor = result.success ? 'var(--fc-state-success)' : 'var(--fc-state-error)';
  const bgColor = result.success ? 'var(--fc-glow-success)' : 'var(--fc-glow-error)';

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
          <span style={{ fontSize: 10, color: 'var(--fc-text-muted)' }}>
            {timeAgo(result.timestamp)}
          </span>
          <button
            onClick={onDismiss}
            style={{
              background: 'none',
              border: 'none',
              color: 'var(--fc-text-muted)',
              cursor: 'pointer',
              fontSize: 14,
              padding: 0,
              lineHeight: 1,
            }}
          >
            x
          </button>
        </div>
      </div>

      {result.error && (
        <div style={{ color: 'var(--fc-state-error)', fontFamily: 'monospace', fontSize: 11, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
          {result.error}
        </div>
      )}

      {result.output && (
        <div style={{
          fontFamily: 'monospace',
          fontSize: 11,
          color: 'var(--fc-text)',
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
              background: 'var(--fc-glow-warning-soft)',
              border: '1px solid var(--fc-glow-warning)',
              borderRadius: 3,
              padding: '1px 5px',
              fontSize: 10,
              color: 'var(--fc-state-warning)',
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
  { key: 'compact_errors', label: 'Compact Errors' },
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

  const varsYamlInput = useBufferedInput(
    typeof props.vars_yaml === 'string' ? props.vars_yaml : '',
    `${nodeId}:start-vars-yaml`,
    (val) => onPropChange('vars_yaml', val.length > 0 ? val : undefined),
  );

  const importsYamlInput = useBufferedInput(
    typeof props.imports_yaml === 'string' ? props.imports_yaml : '',
    `${nodeId}:start-imports-yaml`,
    (val) => onPropChange('imports_yaml', val.length > 0 ? val : undefined),
  );

  const subroutinesYamlInput = useBufferedInput(
    typeof props.subroutines_yaml === 'string' ? props.subroutines_yaml : '',
    `${nodeId}:start-subroutines-yaml`,
    (val) => onPropChange('subroutines_yaml', val.length > 0 ? val : undefined),
  );

  const colors = { text: 'var(--fc-start-chip-text)', border: 'var(--fc-start-accent)', bg: 'var(--fc-start-grad-to)' };

  const inputStyle: React.CSSProperties = {
    width: '100%',
    padding: '4px 6px',
    background: 'var(--fc-input-bg)',
    border: `1px solid ${mix(colors.border, 27)}`,
    borderRadius: 4,
    color: 'var(--fc-text)',
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
        borderBottom: '1px solid var(--fc-panel-border)',
      }}>
        <span style={{
          background: 'var(--fc-start-accent)',
          color: 'var(--fc-on-accent)',
          fontSize: 10,
          fontWeight: 700,
          padding: '2px 6px',
          borderRadius: 3,
          textTransform: 'uppercase',
        }}>
          START
        </span>
        <span style={{ color: 'var(--fc-text)', fontSize: 12, fontWeight: 600 }}>
          Script Settings
        </span>
      </div>

      {/* Name */}
      <div>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted)', display: 'block', marginBottom: 3 }}>
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
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted)', display: 'block', marginBottom: 3 }}>
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
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted)', display: 'block', marginBottom: 3 }}>
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
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted)', display: 'block', marginBottom: 3 }}>
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
        borderTop: '1px solid var(--fc-panel-border)',
        paddingTop: 10,
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
      }}>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted)' }}>Flags</label>
        {START_BOOL_FIELDS.map((field) => (
          <label
            key={field.key}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              fontSize: 12,
              color: 'var(--fc-text-secondary)',
              cursor: 'pointer',
            }}
          >
            <input
              data-testid={`start-${field.key}-input`}
              type="checkbox"
              checked={!!props[field.key]}
              onChange={(e) => onPropChange(field.key, e.target.checked)}
              style={{ accentColor: 'var(--fc-start-accent)' }}
            />
            {field.label}
          </label>
        ))}
      </div>

      {/* Advanced YAML sections */}
      <div style={{
        borderTop: '1px solid var(--fc-panel-border)',
        paddingTop: 10,
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
      }}>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted)' }}>Advanced Sections (YAML)</label>

        <div>
          <label style={{ fontSize: 11, color: 'var(--fc-text-muted)', display: 'block', marginBottom: 3 }}>
            vars
          </label>
          <textarea
            data-testid="start-vars-yaml-input"
            value={varsYamlInput.value}
            placeholder={`key: value\nother: 123`}
            onChange={(e) => varsYamlInput.onChange(e.target.value)}
            onFocus={varsYamlInput.onFocus}
            onBlur={varsYamlInput.onBlur}
            rows={4}
            style={{ ...inputStyle, resize: 'vertical', fontFamily: 'monospace' }}
          />
        </div>

        <div>
          <label style={{ fontSize: 11, color: 'var(--fc-text-muted)', display: 'block', marginBottom: 3 }}>
            imports
          </label>
          <textarea
            data-testid="start-imports-yaml-input"
            value={importsYamlInput.value}
            placeholder={`- path: C:\\\\scripts\\\\shared.yaml\n  as: shared`}
            onChange={(e) => importsYamlInput.onChange(e.target.value)}
            onFocus={importsYamlInput.onFocus}
            onBlur={importsYamlInput.onBlur}
            rows={4}
            style={{ ...inputStyle, resize: 'vertical', fontFamily: 'monospace' }}
          />
        </div>

        <div>
          <label style={{ fontSize: 11, color: 'var(--fc-text-muted)', display: 'block', marginBottom: 3 }}>
            subroutines
          </label>
          <textarea
            data-testid="start-subroutines-yaml-input"
            value={subroutinesYamlInput.value}
            placeholder={`name:\n  steps:\n    - print: "hello"`}
            onChange={(e) => subroutinesYamlInput.onChange(e.target.value)}
            onFocus={subroutinesYamlInput.onFocus}
            onBlur={subroutinesYamlInput.onBlur}
            rows={6}
            style={{ ...inputStyle, resize: 'vertical', fontFamily: 'monospace' }}
          />
        </div>
      </div>

      {/* Read-only summaries for vars and imports */}
      {(varsCount > 0 || importsCount > 0) && (
        <div style={{
          borderTop: '1px solid var(--fc-panel-border)',
          paddingTop: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 4,
        }}>
          {varsCount > 0 && (
            <div style={{ fontSize: 11, color: 'var(--fc-text-muted)' }}>
              {varsCount} variable{varsCount !== 1 ? 's' : ''} defined
            </div>
          )}
          {importsCount > 0 && (
            <div style={{ fontSize: 11, color: 'var(--fc-text-muted)' }}>
              {importsCount} import{importsCount !== 1 ? 's' : ''}
            </div>
          )}
        </div>
      )}

      {/* Footer */}
      <div style={{
        marginTop: 'auto',
        paddingTop: 12,
        borderTop: '1px solid var(--fc-panel-border)',
        fontSize: 11,
        color: 'var(--fc-text-muted)',
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
        <span style={{ color: 'var(--fc-text-muted)', fontSize: 12, textAlign: 'center' }}>
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
        <span style={{ color: 'var(--fc-text-muted)', fontSize: 12, textAlign: 'center' }}>
          Select a block to edit its properties
        </span>
      </div>
    );
  }

  const colors = categoryColors[def.category as BlockCategory];
  const branchLabel = blockData.props?.['_branchLabel'] as string | undefined;
  const branchStepPath = blockData.props?.['_stepPath'] as string | undefined;
  const branchTint = branchColorVar(
    branchStepPath
      ? // reuse the same parse as the band layer via the label as a cheap proxy when present
        (branchLabel ?? branchStepPath)
      : branchLabel,
  );
  const groupedProperties: Record<PropertyPaneGroup, PropertyDef[]> = {
    core: [],
    advanced: [],
    on_error: [],
  };

  for (const propDef of def.properties) {
    groupedProperties[resolvePropertyGroup(propDef)].push(propDef);
  }

  const propertySections: Array<{ key: PropertyPaneGroup; label: string }> = [
    { key: 'core', label: 'Core' },
    { key: 'advanced', label: 'Advanced' },
    { key: 'on_error', label: 'On Error' },
  ];
  const visibleSections = propertySections.filter((section) => groupedProperties[section.key].length > 0);

  const renderProperty = (propDef: PropertyDef) => {
    const fieldTestId = `properties-field-${propDef.key}-${propDef.type}`;
    const fieldValue = resolveDisplayedPropertyValue(blockData.blockType, propDef, blockData.props);
    const required = isPropertyRequired(blockData.blockType, propDef, blockData.props);
    const validationMessage = getPropertyValidationMessage(blockData.blockType, propDef, fieldValue, required);
    const invalid = validationMessage !== null;
    const warningMessage = blockData.blockType === 'choose' && propDef.key === 'default'
      ? getChooseDefaultWarning(fieldValue, blockData.props?.options)
      : null;
    const showInlineError = validationMessage !== null && propDef.editor !== 'choice-options';

    return (
      <div key={`${selectedNodeId}-${propDef.key}`} data-testid={fieldTestId}>
        {propDef.type !== 'boolean' && (
          <label style={{ fontSize: 11, color: 'var(--fc-text-muted)', display: 'block', marginBottom: 3 }}>
            {propDef.label}
            {required && <span style={{ color: 'var(--fc-state-error)', marginLeft: 2 }}>*</span>}
          </label>
        )}
        <PropertyField
          def={propDef}
          value={fieldValue}
          onChange={(val) => updateProp(propDef.key, val)}
          colors={colors}
          fieldTestId={fieldTestId}
          nodeId={selectedNodeId}
          blockType={blockData.blockType}
          required={required}
          invalid={invalid}
        />
        {propDef.helpText && (
          <div style={{ marginTop: 4, fontSize: 10, color: 'var(--fc-text-muted)' }}>
            {propDef.helpText}
          </div>
        )}
        {showInlineError && (
          <div
            data-testid={`${fieldTestId}-error`}
            style={{ marginTop: 4, color: 'var(--fc-state-error)', fontSize: 11 }}
          >
            {validationMessage}
          </div>
        )}
        {warningMessage && (
          <div
            data-testid={`${fieldTestId}-warning`}
            style={{ marginTop: 4, color: 'var(--fc-state-warning)', fontSize: 11 }}
          >
            {warningMessage}
          </div>
        )}
      </div>
    );
  };

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
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, paddingBottom: 8, borderBottom: '1px solid var(--fc-panel-border)' }}>
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
        <span style={{ color: 'var(--fc-text)', fontSize: 12, fontWeight: 600 }}>
          {def.label}
        </span>
      </div>

      {branchLabel && (
        <div style={{
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          padding: '4px 8px',
          background: mix(branchTint, 8),
          borderRadius: 4,
          borderLeft: `2px solid ${branchTint}`,
        }}>
          <span style={{ fontSize: 10, color: branchTint, fontWeight: 600, textTransform: 'uppercase' }}>
            {branchLabel}
          </span>
          <span style={{ fontSize: 10, color: 'var(--fc-text-muted)' }}>branch</span>
        </div>
      )}

      {visibleSections.map((section, sectionIndex) => {
        const properties = groupedProperties[section.key];
        const isFirstVisibleSection = sectionIndex === 0;
        return (
          <section
            key={section.key}
            style={{
              display: 'flex',
              flexDirection: 'column',
              gap: 8,
              paddingTop: isFirstVisibleSection ? 0 : 8,
              borderTop: isFirstVisibleSection ? undefined : '1px solid var(--fc-panel-border)',
            }}
          >
            <div
              style={{
                fontSize: 10,
                color: 'var(--fc-text-muted)',
                textTransform: 'uppercase',
                fontWeight: 600,
                letterSpacing: '0.04em',
              }}
            >
              {section.label}
            </div>
            {properties.map((propDef) => renderProperty(propDef))}
          </section>
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
        borderTop: '1px solid var(--fc-panel-border)',
        fontSize: 11,
        color: 'var(--fc-text-muted)',
        lineHeight: 1.5,
      }}>
        {def.description}
      </div>
    </div>
  );
}


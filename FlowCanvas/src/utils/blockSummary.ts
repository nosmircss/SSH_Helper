// FlowCanvas/src/utils/blockSummary.ts
import { blockDefMap, type PropertyDef } from '../blockDefs/registry';

export interface SummaryRow {
  key: string; label: string; value: string;
  isCode: boolean; masked: boolean; notSet: boolean;
}
export interface BlockSummary { rows: SummaryRow[]; hiddenCount: number; }

const SECRET_KEYS = new Set(['password', 'token']);

function toBool(v: unknown, d: boolean): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'string') return v.trim().toLowerCase() === 'true';
  return d;
}
function hasValue(v: unknown): boolean {
  return v !== undefined && v !== null && String(v).trim() !== '';
}

/** Required logic ported from Properties.isPropertyRequired (conditional cases preserved). */
function isRequired(blockType: string, def: PropertyDef, props: Record<string, unknown>): boolean {
  const base = !!def.required;
  if (blockType === 'readfile' && def.key === 'path') return !toBool(props.select_file, false);
  if (blockType === 'readfile' && def.key === 'into') return !toBool(props.path_only, false);
  if (blockType === 'readfile' && def.key === 'path_into') return toBool(props.path_only, false);
  if (blockType === 'http') {
    const auth = String(props.auth ?? 'none').trim().toLowerCase();
    if (def.key === 'username' || def.key === 'password') return auth === 'basic';
    if (def.key === 'token') return auth === 'bearer';
    return base;
  }
  if (blockType === 'interactive') {
    const showWindow = toBool(props.show_window, true);
    if (showWindow) return base;
    if (def.key === 'command') return true;
    if (def.key === 'max_seconds' || def.key === 'max_lines') {
      // Window hidden → a bound is required: max_seconds OR max_lines must be set.
      return !hasValue(props.max_seconds) && !hasValue(props.max_lines);
    }
  }
  return base;
}

/** A field is "non-default" if it has a value that differs from its declared defaultValue. */
function isNonDefault(def: PropertyDef, props: Record<string, unknown>): boolean {
  const v = props[def.key];
  if (!hasValue(v)) return false;
  if (def.defaultValue === undefined || def.defaultValue === null) return true;
  return String(v) !== String(def.defaultValue);
}

export function summarizeBlock(blockType: string, props: Record<string, unknown>): BlockSummary {
  const def = blockDefMap.get(blockType);
  if (!def) return { rows: [], hiddenCount: 0 };
  const rows: SummaryRow[] = [];
  let hidden = 0;
  for (const p of def.properties) {
    const required = isRequired(blockType, p, props);
    const nonDefault = isNonDefault(p, props);
    if (!required && !nonDefault) { hidden++; continue; }
    const raw = props[p.key];
    const notSet = required && !hasValue(raw);
    rows.push({
      key: p.key, label: p.label,
      value: notSet ? '— not set' : (SECRET_KEYS.has(p.key) ? '••••••••' : String(raw)),
      isCode: p.type === 'code',
      masked: SECRET_KEYS.has(p.key) && hasValue(raw),
      notSet,
    });
  }
  return { rows, hiddenCount: hidden };
}

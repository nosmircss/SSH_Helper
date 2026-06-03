import { memo, type CSSProperties, useCallback, useEffect, useState } from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import { blockDefMap, categoryColors, type BlockCategory } from '../blockDefs/registry';
import { useFlowStore } from '../stores/useFlowStore';
import { mix } from '../utils/tokens';
import { nodeBorderColor, resolveNodeShadow } from '../utils/nodeStyle';
import { summarizeBlock } from '../utils/blockSummary';
import { SPINE_WIDTH, CHILD_WIDTH } from '../utils/nodeSize';
import { BlockIcon } from './BlockIcon';
import './baseblock.css';
import './execution-cinematics.css';

export interface BlockNodeData {
  blockType: string;
  label?: string;
  /** Key-value properties for this block instance */
  props?: Record<string, unknown>;
  /** Execution state: idle | running | success | error | skipped | disabled */
  execState?: 'idle' | 'running' | 'success' | 'error' | 'skipped' | 'disabled';
  /** Whether a breakpoint is set on this block */
  breakpoint?: boolean;
  [key: string]: unknown;
}

// Token-driven heat ramp (Decision #4: no inline hex). cold→mid→hot interpolated
// in OKLCH via color-mix so blocks tint by their relative run duration.
function heatColor(ratio: number): string {
  const r = Math.max(0, Math.min(1, ratio));
  const from = r < 0.5 ? 'var(--fc-heat-cold)' : 'var(--fc-heat-mid)';
  const to = r < 0.5 ? 'var(--fc-heat-mid)' : 'var(--fc-heat-hot)';
  const pct = Math.round((r < 0.5 ? r * 2 : (r - 0.5) * 2) * 100);
  return `color-mix(in oklch, ${to} ${pct}%, ${from})`;
}

// Single source for the duration format (sub-second → "Nms", else "N.Ns") — shared by the
// settled badge and the live ticker so the value can't drift between running and done.
function formatDuration(ms: number): string {
  return ms < 1000 ? `${ms}ms` : `${(ms / 1000).toFixed(1)}s`;
}

// Maps a runtime branch scope-key (matching edge.data.branchPath) to a short badge label.
function deriveBranchLabel(key: string): string {
  if (key === 'then' || key === 'else' || key === 'default') return key;
  const elif = key.match(/^elif\/(\d+)/);
  if (elif) return `elif #${Number(elif[1]) + 1}`;
  const c = key.match(/^cases\/(\d+)/);
  if (c) return `case #${Number(c[1]) + 1}`;
  return key;
}

// Live elapsed ticker: while a block runs, formats `now - start` via requestAnimationFrame,
// re-rendering only when the formatted text changes. Returns null when not running, under reduced
// motion, or before `start` is known — the badge then falls back to the settled duration.
function useRunningElapsed(start: number | undefined, isRunning: boolean, reducedMotion: boolean): string | null {
  const [text, setText] = useState<string | null>(null);
  useEffect(() => {
    if (!isRunning || reducedMotion || start == null) {
      setText(null);
      return;
    }
    let raf = 0;
    let last = '';
    const tick = () => {
      const next = formatDuration(Date.now() - start);
      if (next !== last) {
        last = next;
        setText(next);
      }
      raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [start, isRunning, reducedMotion]);
  return text;
}

function BaseBlock({ data, selected, id }: NodeProps) {
  const blockData = data as BlockNodeData;
  const def = blockDefMap.get(blockData.blockType);
  const toggleBreakpoint = useFlowStore((s) => s.toggleBreakpoint);
  const blockTimings = useFlowStore((s) => s.blockTimings);
  const heatmapEnabled = useFlowStore((s) => s.heatmapEnabled);
  const reducedMotion = useFlowStore((s) => s.reducedMotion);
  const maxDuration = useFlowStore((s) => {
    // Heatmap off → skip the whole-map scan. heatTint (the only consumer) is gated on
    // heatmapEnabled too, so 0 is never observed while the heatmap is on. Returning a
    // primitive lets Object.is short-circuit re-renders.
    if (!s.heatmapEnabled) return 0;
    let max = 0;
    s.blockTimings.forEach((t) => { if (t.duration && t.duration > max) max = t.duration; });
    return max;
  });
  const loopIteration = useFlowStore((s) => s.loopIterations.get(id));
  const branchTakenKey = useFlowStore((s) => s.branchTaken.get(id));
  const isExpanded = useFlowStore((s) => s.isExpanded(id));
  const toggleExpanded = useFlowStore((s) => s.toggleExpanded);
  const selectNode = useFlowStore((s) => s.selectNode);

  const handleBreakpointToggle = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    toggleBreakpoint(id);
  }, [id, toggleBreakpoint]);

  // Live ticker — a hook, so it MUST run before the early return below (rules of hooks). It reads
  // `start` directly off the timings Map; `timing` itself stays declared in the badge block below.
  const liveText = useRunningElapsed(blockTimings.get(id)?.start, blockData.execState === 'running', reducedMotion);

  if (!def) return <div style={{ color: 'var(--fc-state-error-text)' }}>Unknown: {blockData.blockType}</div>;

  const colors = categoryColors[def.category as BlockCategory];
  const execState = blockData.execState || 'idle';
  const hasBreakpoint = blockData.breakpoint;
  const isChild = !!blockData.props?.['_isChildOf'];
  const isDisabled = execState === 'disabled';

  // Duration badge: settled value after completion. While running, the live ticker (read above)
  // drives the badge; on completion it locks to the measured duration.
  const timing = blockTimings.get(id);
  const durationMs = timing?.duration;
  const durationText = durationMs != null ? formatDuration(durationMs) : null;
  const badgeText = execState === 'running' ? liveText : durationText;

  // Run-heatmap tint: only on idle/success blocks so it never overrides the
  // running pulse or the error glow (precedence). Render-time only — never
  // written onto node.data, so the graph snapshot/export stays unchanged.
  const heatTint = (heatmapEnabled && durationMs != null && maxDuration > 0
    && (execState === 'idle' || execState === 'success'))
    ? heatColor(durationMs / maxDuration)
    : undefined;

  // Preview text should follow live editable props for this block type.
  // _preview is importer metadata and can become stale after local edits.
  const previewText = def.previewKey
    ? blockData.props?.[def.previewKey] !== undefined && blockData.props?.[def.previewKey] !== null
      ? String(blockData.props[def.previewKey])
      : null
    : blockData.props?.['_preview']
      ? String(blockData.props['_preview'])
      : null;

  const summary = isExpanded
    ? summarizeBlock(blockData.blockType, (blockData.props ?? {}) as Record<string, unknown>)
    : null;

  // running + error are class-driven: the fc-exec-running / fc-exec-error animations own the
  // box-shadow via the cascade (CSS animations outrank inline styles), so no inline glow here.
  // success/skipped settle to a soft static glow on the INLINE path so the heat ring still stacks;
  // idle gets the category "neon ring" (gated off when the heat ring is active). See utils/nodeStyle.
  const heatActive = heatTint != null;
  const existingBoxShadow = resolveNodeShadow({
    execState,
    selected,
    heatActive,
    border: colors.border,
  });

  const containerStyle: CSSProperties = {
    background: isDisabled ? 'var(--fc-surface-disabled)' : 'var(--fc-node-surface)',
    border: `1px solid ${nodeBorderColor({ selected, isDisabled, border: colors.border })}`,
    borderRadius: 8,
    minWidth: isChild ? CHILD_WIDTH : SPINE_WIDTH,
    maxWidth: isChild ? CHILD_WIDTH : SPINE_WIDTH,
    overflow: 'hidden',
    opacity: isDisabled ? 0.5 : isChild ? 0.95 : 1,
    boxShadow: heatTint ? `0 0 0 3px ${heatTint}, ${existingBoxShadow}` : existingBoxShadow,
    transition: 'box-shadow 0.2s, border-color 0.2s, opacity 0.2s',
    position: 'relative',
  };

  // running + error get a state class whose CSS animation owns the card's box-shadow/transform
  // (breathing glow / shake+ripple). success + skipped stay on the inline box-shadow path.
  const stateClass = execState === 'running' ? 'fc-exec-running'
    : execState === 'error' ? 'fc-exec-error'
      : undefined;

  // Category-tinted icon chip. color tints the stroke (currentColor); a faint category wash sits
  // behind it. mix() is the gate-safe color-mix helper — no new per-category token needed.
  const iconChipStyle: CSSProperties = {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    width: 20,
    height: 20,
    flexShrink: 0,
    borderRadius: 4,
    color: isDisabled ? 'var(--fc-text-faint)' : colors.icon,
    background: isDisabled ? 'transparent' : mix(colors.border, def.isContainer ? 20 : 14),
  };

  const headerStyle: CSSProperties = {
    padding: '6px 9px',
    borderBottom: `1px solid ${mix(colors.border, 20)}`,
    display: 'flex',
    alignItems: 'center',
    gap: 6,
    fontSize: 'var(--fc-fs-header)',
  };

  const badgeStyle: CSSProperties = {
    background: 'transparent',
    color: isDisabled ? 'var(--fc-text-secondary)' : colors.text,
    fontSize: 10,
    fontWeight: 700,
    padding: '2px 6px',
    borderRadius: 3,
    border: `1px solid ${mix(colors.border, 40)}`,
    textTransform: 'uppercase',
    letterSpacing: '0.5px',
    flexShrink: 0,
  };

  const execIndicator = execState !== 'idle' && execState !== 'disabled' ? (
    <span style={{
      fontSize: 9,
      marginLeft: 'auto',
      display: 'flex',
      alignItems: 'center',
      gap: 3,
      color: execState === 'running' ? 'var(--fc-accent)'
        : execState === 'success' ? 'var(--fc-state-success)'
        : execState === 'skipped' ? 'var(--fc-text-secondary)'
        : 'var(--fc-state-error)',
      fontWeight: 600,
    }}>
      {execState === 'running' ? 'RUNNING'
        : execState === 'success' ? (
          <>
            <svg className="fc-check" viewBox="0 0 24 24" width="11" height="11" aria-hidden="true">
              <path d="M5 13l4 4L19 7" pathLength={1} />
            </svg>
            DONE
          </>
        )
        : execState === 'skipped' ? '— SKIP'
        : '✗ ERROR'}
      {badgeText && (
        <span data-testid="exec-duration-badge" style={{
          fontSize: 8,
          color: 'var(--fc-text-secondary)',
          background: 'var(--fc-surface-0)',
          padding: '1px 4px',
          borderRadius: 3,
          marginLeft: 2,
        }}>
          {badgeText}
        </span>
      )}
      {loopIteration != null && (
        <span data-testid="exec-loop-badge" style={{
          fontSize: 8,
          color: 'var(--fc-text-secondary)',
          background: 'var(--fc-surface-0)',
          padding: '1px 4px',
          borderRadius: 3,
          marginLeft: 2,
        }}>
          ×{loopIteration}
        </span>
      )}
      {branchTakenKey && (
        <span data-testid="exec-branch-badge" style={{
          fontSize: 8,
          color: 'var(--fc-text-secondary)',
          background: 'var(--fc-surface-0)',
          padding: '1px 4px',
          borderRadius: 3,
          marginLeft: 2,
        }}>
          {deriveBranchLabel(branchTakenKey)}
        </span>
      )}
    </span>
  ) : isDisabled ? (
    <span style={{ fontSize: 9, marginLeft: 'auto', color: 'var(--fc-text-faint)', fontWeight: 600 }}>
      ⏭ DISABLED
    </span>
  ) : null;

  return (
    <div className={stateClass} style={containerStyle} data-testid="block-node">
      {/* Running comet halo: a sweeping conic ring on the card edge. Render-only and gated by
          reduced motion (no comet, no churn when motion is off). inset:0 keeps it inside the
          border-box so it never grows the node or gets clipped. */}
      {execState === 'running' && !reducedMotion && (
        <span className="fc-run-halo" aria-hidden="true" />
      )}

      {/* Input handle (top) */}
      <Handle
        type="target"
        position={Position.Top}
        style={{ background: colors.border, width: 8, height: 8, border: 'none' }}
      />

      {/* Header */}
      <div style={headerStyle}>
        {/* Breakpoint gutter */}
        {!isChild && (
          <span
            onClick={handleBreakpointToggle}
            style={{
              width: 10, height: 10, borderRadius: '50%',
              background: hasBreakpoint ? 'var(--fc-state-error)' : 'transparent',
              border: hasBreakpoint ? 'none' : '1px solid var(--fc-border-subtle)',
              flexShrink: 0,
              cursor: 'pointer',
              boxShadow: hasBreakpoint ? '0 0 4px var(--fc-glow-error)' : 'none',
              transition: 'background 0.15s',
            }}
            title="Toggle breakpoint"
          />
        )}

        {/* Category-tinted icon chip */}
        <span style={iconChipStyle}>
          <BlockIcon name={def.icon} />
        </span>

        <span style={badgeStyle}>{def.type}</span>
        <span style={{
          color: isDisabled ? 'var(--fc-text-faint)' : 'var(--fc-text)',
          fontSize: 13,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
          textDecoration: isDisabled ? 'line-through' : 'none',
        }}>
          {blockData.label || def.label}
        </span>
        {execIndicator}
        <span
          data-testid="expand-toggle"
          onClick={(e) => { e.stopPropagation(); toggleExpanded(id); }}
          style={{ marginLeft: 4, cursor: 'pointer', color: 'var(--fc-text-secondary)', display: 'flex' }}
          title={isExpanded ? 'Collapse' : 'Expand settings'}
        >
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <polyline points={isExpanded ? '6 9 12 15 18 9' : '9 6 15 12 9 18'} />
          </svg>
        </span>
      </div>

      {/* Preview content or expanded summary */}
      {isExpanded && summary ? (
        <div data-testid="block-summary" style={{ background: 'var(--fc-surface-0)', padding: '8px 9px 6px' }}>
          {summary.rows.map((r) => (
            <div key={r.key} style={{ display: 'flex', gap: 10, padding: '3px 0', alignItems: 'baseline' }}>
              <span style={{ flex: 'none', width: 96, fontSize: 10.5, fontWeight: 600, color: 'var(--fc-text-secondary)' }}>{r.label}</span>
              <span style={{
                flex: 1, fontSize: 11.5, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                fontFamily: r.isCode ? 'var(--fc-font-mono)' : undefined,
                color: r.notSet ? 'var(--fc-text-faint)' : (r.isCode ? colors.text : 'var(--fc-text)'),
              }}>{r.value}</span>
            </div>
          ))}
          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 5, paddingTop: 6,
                        borderTop: '1px solid var(--fc-border)', fontSize: 10 }}>
            <span style={{ color: 'var(--fc-text-faint)' }}>
              {summary.hiddenCount} fields at default
            </span>
            <span
              onClick={(e) => { e.stopPropagation(); selectNode(id); }}
              style={{ color: 'var(--fc-accent)', cursor: 'pointer' }}
            >Edit in Properties</span>
          </div>
        </div>
      ) : previewText ? (
        <div style={{ padding: '4px 8px', fontFamily: 'monospace', fontSize: 12,
          color: isDisabled ? 'var(--fc-text-disabled)' : colors.text,
          overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {previewText}
        </div>
      ) : null}

      {/* Output handle (bottom). For a CONTAINER this is the THEN/body branch source and the
          continuation diamond sits at bottom-center, so shift this handle right (toward the indented
          body) to keep the two from stacking. A plain block's single successor stays centered so its
          spine edge renders as a straight vertical line. */}
      <Handle
        type="source"
        position={Position.Bottom}
        style={{
          background: colors.border, width: 8, height: 8, border: 'none',
          ...(def.isContainer ? { left: '75%' } : {}),
        }}
      />

      {/* Second output for IF blocks */}
      {def.outputs === 2 && (
        <Handle
          type="source"
          position={Position.Right}
          id="false"
          style={{
            background: 'var(--fc-state-error)', width: 8, height: 8, border: 'none',
            top: '50%',
          }}
        />
      )}

      {/* Continuation handle for container blocks (accent diamond). EVERY container now indents its
          branches clear of the spine, so the continuation leaves the bottom-CENTER and runs straight
          down the spine gutter. A centered, NON-rotated marker keeps React Flow's connection point
          dead-center — a rotate(45deg) diamond inflates the bounding box and offsets the point ~2px,
          enough to fail the isSpine test and bend the "straight" continuation (proven via the
          edge-geometry e2e). */}
      {def.isContainer && (
        <Handle
          type="source"
          position={Position.Bottom}
          id="continue"
          style={{ background: 'var(--fc-accent)', width: 10, height: 10, border: 'none', borderRadius: 3 }}
        />
      )}
    </div>
  );
}

export default memo(BaseBlock);

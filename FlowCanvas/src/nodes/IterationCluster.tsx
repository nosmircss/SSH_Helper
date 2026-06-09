import { useMemo, type CSSProperties } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { selectVisibleIterations } from '../stores/selectors/iterationScope';
import { mix } from '../utils/tokens';
import type { BranchBand } from '../utils/branchBands';

/** Scrubber appears when the visible iteration count exceeds this. */
const SCRUBBER_THRESHOLD = 20;
/** Above this many ticks, each tick becomes a bucket of iterations. */
const MAX_TICKS = 60;
/** Horizontal clearances along the band's top edge (px): past the LOOP pill / reserved for the cluster. */
const SCRUB_LEFT = 86;
const SCRUB_RIGHT_RESERVE = 230;

const chip: CSSProperties = {
  borderRadius: 999, padding: '2px 7px', cursor: 'pointer',
  font: 'inherit', letterSpacing: '0.05em',
};
const arrow: CSSProperties = {
  background: 'none', border: 'none', cursor: 'pointer',
  color: 'var(--fc-accent)', font: 'inherit', padding: '0 2px',
};

interface IterationClusterProps {
  band: BranchBand;
}

/**
 * Post-run iteration stepper pinned to a loop band's top-right:
 *   [ALL] [◀ web-02 · 3/12 ▶] [⚠ 2]
 * Stepping re-scopes the path overlay, badges, durations, and Block Output to one
 * iteration (selectors do the scoping; this control only drives iterationSelections).
 */
export default function IterationCluster({ band }: IterationClusterProps) {
  const loopId = band.parentId;
  const isRunning = useFlowStore((s) => s.isRunning);
  // Subscribe to the stable map references; derive the (fresh-array) visible list in a
  // memo so the zustand snapshot stays referentially stable between store changes.
  const log = useFlowStore((s) => s.iterationLog);
  const sels = useFlowStore((s) => s.iterationSelections);
  const nodes = useFlowStore((s) => s.nodes);
  const total = useFlowStore((s) => s.totalIterations.get(loopId) ?? 0);
  const setSelection = useFlowStore((s) => s.setIterationSelection);

  const visible = useMemo(
    () => selectVisibleIterations(useFlowStore.getState(), loopId),
    [log, sels, nodes, loopId],
  );

  if (isRunning || visible.length === 0) return null;

  const selection = sels.get(loopId) ?? null;
  const pos = selection == null ? -1 : visible.findIndex((r) => r.seq === selection);
  const current = pos >= 0 ? visible[pos] : null;
  const failedCount = visible.filter((r) => r.failed).length;
  const kept = log.get(loopId)?.length ?? 0;
  const evicted = total > kept;

  const step = (delta: 1 | -1) => {
    const next = pos < 0
      ? (delta > 0 ? 0 : visible.length - 1)
      : Math.min(visible.length - 1, Math.max(0, pos + delta));
    setSelection(loopId, visible[next].seq);
  };

  const jumpFailed = () => {
    if (failedCount === 0) return;
    for (let k = 1; k <= visible.length; k++) {
      const idx = ((pos < 0 ? -1 : pos) + k + visible.length) % visible.length;
      if (visible[idx].failed) { setSelection(loopId, visible[idx].seq); return; }
    }
  };

  const label = current ? (current.label ?? `#${current.i + 1}`) : null;
  const counter = pos < 0 ? `${visible.length}` : `${pos + 1}/${visible.length}`;

  const showScrubber = visible.length > SCRUBBER_THRESHOLD;
  const bucketSize = showScrubber ? Math.ceil(visible.length / MAX_TICKS) : 1;
  const buckets: { startPos: number; failed: boolean; active: boolean }[] = [];
  if (showScrubber) {
    for (let b = 0; b * bucketSize < visible.length; b++) {
      const start = b * bucketSize;
      const end = Math.min(visible.length, start + bucketSize);
      buckets.push({
        startPos: start,
        failed: visible.slice(start, end).some((r) => r.failed),
        active: pos >= start && pos < end,
      });
    }
  }

  return (
    <>
      {showScrubber && (
        <div
          data-testid="iter-scrubber"
          onPointerDown={(e) => e.stopPropagation()}
          style={{
            position: 'absolute',
            transform: `translate(${band.x + SCRUB_LEFT}px, ${band.y - 8}px)`,
            width: Math.max(60, band.width - SCRUB_LEFT - SCRUB_RIGHT_RESERVE),
            height: 15, display: 'flex', alignItems: 'center', gap: 1,
            background: 'var(--fc-surface-0)',
            border: `1px solid ${mix(band.colorVar, 45)}`,
            borderRadius: 999, padding: '0 6px',
            zIndex: 6, pointerEvents: 'auto',
          }}
        >
          {buckets.map((bk, idx) => (
            <span
              key={idx}
              data-testid="iter-tick"
              onClick={() => setSelection(loopId, visible[bk.startPos].seq)}
              title={`${visible[bk.startPos].label ?? `#${visible[bk.startPos].i + 1}`} · ${bk.startPos + 1}/${visible.length}`}
              style={{
                flex: 1, minWidth: 2, borderRadius: 2, cursor: 'pointer',
                height: bk.active ? 11 : bk.failed ? 8 : 6,
                background: bk.active
                  ? 'var(--fc-edge-traversed)'
                  : bk.failed
                    ? 'var(--fc-state-error)'
                    : mix(band.colorVar, 35),
              }}
            />
          ))}
        </div>
      )}
      <div
        data-testid="iteration-cluster"
        // Lives inside the ViewportPortal (the React Flow pane): without this, a click on any
        // chip bubbles to the pane, clearing the selection / arming box-select (same guard as
        // the band drag handle in BranchBandsLayer).
        onPointerDown={(e) => e.stopPropagation()}
        style={{
          position: 'absolute',
          transform: `translate(calc(${band.x + band.width - 8}px - 100%), ${band.y - 11}px)`,
          display: 'flex', alignItems: 'center', gap: 4,
          zIndex: 6, pointerEvents: 'auto',
          font: '600 9px/1.4 system-ui, sans-serif',
        }}
      >
      <button
        data-testid="iter-all"
        onClick={() => setSelection(loopId, null)}
        title="Show all iterations (aggregate view)"
        style={{
          ...chip,
          color: pos < 0 ? 'oklch(17% 0.02 275)' : 'var(--fc-text-secondary)',
          background: pos < 0 ? band.colorVar : 'var(--fc-surface-0)',
          border: `1px solid ${mix(band.colorVar, 45)}`,
        }}
      >
        ALL
      </button>
      <span style={{
        display: 'inline-flex', alignItems: 'center', gap: 4,
        background: 'var(--fc-surface-0)',
        border: `1px solid ${mix(band.colorVar, 45)}`,
        borderRadius: 999, padding: '2px 6px',
      }}>
        <button data-testid="iter-prev" onClick={() => step(-1)} style={arrow} title="Previous iteration">◀</button>
        {label && (
          <span
            data-testid="iter-label"
            title={label}
            style={{
              fontFamily: 'Consolas, monospace', color: 'var(--fc-edge-traversed)',
              maxWidth: 90, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
            }}
          >
            {label}
          </span>
        )}
        <span data-testid="iter-counter" style={{ color: 'oklch(88% 0.02 275)', fontVariantNumeric: 'tabular-nums' }}>
          {counter}
        </span>
        <button data-testid="iter-next" onClick={() => step(1)} style={arrow} title="Next iteration">▶</button>
      </span>
      {failedCount > 0 && (
        <button
          data-testid="iter-fail"
          onClick={jumpFailed}
          title={`Jump to next failed iteration (${failedCount} failed)`}
          style={{
            ...chip,
            color: 'var(--fc-state-error)', background: 'var(--fc-surface-0)',
            border: '1px solid color-mix(in oklch, var(--fc-state-error) 55%, transparent)',
          }}
        >
          ⚠ {failedCount}
        </button>
      )}
      {evicted && (
        <span data-testid="iter-evicted" style={{ color: 'var(--fc-text-secondary)' }}>
          of {total.toLocaleString()}
        </span>
      )}
    </div>
    </>
  );
}

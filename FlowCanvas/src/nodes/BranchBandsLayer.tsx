// FlowCanvas/src/nodes/BranchBandsLayer.tsx
import { ViewportPortal, useReactFlow } from '@xyflow/react';
import { useRef, type PointerEvent } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { computeBranchBands, branchPillLabel, type BranchBand } from '../utils/branchBands';
import { BLOCK_WIDTH_INSET } from '../utils/nodeSize';
import { mix } from '../utils/tokens';
import { sendLayoutAutosave } from '../utils/layoutAutosave';
import IterationCluster from './IterationCluster';
import './bandlayer.css';

export default function BranchBandsLayer() {
  const nodes = useFlowStore((s) => s.nodes);
  const enabled = useFlowStore((s) => s.branchBandsEnabled);
  const blockWidth = useFlowStore((s) => s.blockWidth);
  const { screenToFlowPosition } = useReactFlow();
  const drag = useRef<{ memberIds: string[]; lastX: number; lastY: number } | null>(null);

  if (!enabled) return null;
  const bands = computeBranchBands(nodes, blockWidth - BLOCK_WIDTH_INSET);
  if (bands.length === 0) return null;

  const startDrag = (e: PointerEvent<HTMLDivElement>, band: BranchBand) => {
    if (e.button !== 0) return; // left button only — middle/right still pan the canvas
    e.stopPropagation();
    e.currentTarget.setPointerCapture(e.pointerId);
    useFlowStore.getState().pushSnapshot('Move band');
    const p = screenToFlowPosition({ x: e.clientX, y: e.clientY });
    drag.current = { memberIds: band.memberIds, lastX: p.x, lastY: p.y };
  };

  const moveDrag = (e: PointerEvent<HTMLDivElement>) => {
    const d = drag.current;
    if (!d) return;
    const p = screenToFlowPosition({ x: e.clientX, y: e.clientY });
    const dx = p.x - d.lastX;
    const dy = p.y - d.lastY;
    if (dx === 0 && dy === 0) return;
    d.lastX = p.x;
    d.lastY = p.y;
    useFlowStore.getState().translateNodesBy(d.memberIds, dx, dy);
  };

  const endDrag = (e: PointerEvent<HTMLDivElement>) => {
    if (!drag.current) return;
    drag.current = null;
    try { e.currentTarget.releasePointerCapture(e.pointerId); } catch { /* already released */ }
    sendLayoutAutosave();
  };

  return (
    <ViewportPortal>
      {/* Band rectangles: behind nodes, non-interactive (geometry/look unchanged). */}
      {bands.map((b) => {
        const nested = b.depth >= 1;
        const tint = nested ? 13 : 7;
        return (
          <div
            key={b.id}
            data-testid="branch-band"
            data-branch={b.branchKey}
            style={{
              position: 'absolute',
              transform: `translate(${b.x}px, ${b.y}px)`,
              width: b.width, height: b.height,
              background: mix(b.colorVar, tint),
              // Longhand per-side borders (not the `border` shorthand) so the 3px left accent
              // can't be clobbered by the shorthand on rerender — React warns about mixing them.
              borderTop: `1px solid ${mix(b.colorVar, nested ? 55 : 38)}`,
              borderRight: `1px solid ${mix(b.colorVar, nested ? 55 : 38)}`,
              borderBottom: `1px solid ${mix(b.colorVar, nested ? 55 : 38)}`,
              borderLeft: `3px solid ${mix(b.colorVar, 70)}`,
              borderRadius: 9,
              pointerEvents: 'none',
              zIndex: -1,
            }}
          />
        );
      })}

      {/* Draggable label handles: rendered as siblings (not children) of the rectangles so their
          zIndex isn't trapped by the rectangle's zIndex:-1 stacking context. They sit in the band's
          top headroom, above the pane, and catch the pointer to move the whole band. */}
      {bands.map((b) => {
        const nested = b.depth >= 1;
        return (
          <div
            key={`${b.id}::handle`}
            data-testid="branch-band-handle"
            data-branch={b.branchKey}
            className="fc-band-handle"
            title="Drag to move this band"
            onPointerDown={(e) => startDrag(e, b)}
            onPointerMove={moveDrag}
            onPointerUp={endDrag}
            onLostPointerCapture={endDrag}
            style={{
              position: 'absolute',
              transform: `translate(${b.x}px, ${b.y}px)`,
              font: '800 9px/1.4 system-ui, sans-serif', letterSpacing: '0.08em',
              padding: '2px 10px', borderRadius: '9px 0 8px 0',
              color: 'oklch(17% 0.02 275)',
              background: nested ? `color-mix(in oklch, ${b.colorVar}, white 14%)` : b.colorVar,
              display: 'inline-flex', alignItems: 'center', gap: '4px',
              cursor: 'grab', pointerEvents: 'auto', userSelect: 'none',
              zIndex: 5,
            }}
          >
            <span className="fc-band-grip" aria-hidden="true">⠿</span>
            {branchPillLabel(b.branchKey)}
          </div>
        );
      })}

      {/* Iteration stepper clusters: one per loop band, top-right. Post-run only —
          the component returns null while running or with no recorded iterations.
          Sibling of the rectangles/handles so zIndex isn't trapped (same precedent). */}
      {bands.filter((b) => b.branchKey === 'do').map((b) => (
        <IterationCluster key={`${b.id}::iters`} band={b} />
      ))}
    </ViewportPortal>
  );
}

// FlowCanvas/src/nodes/BranchBandsLayer.tsx
import { ViewportPortal } from '@xyflow/react';
import { useFlowStore } from '../stores/useFlowStore';
import { computeBranchBands, branchPillLabel } from '../utils/branchBands';
import { mix } from '../utils/tokens';

export default function BranchBandsLayer() {
  const nodes = useFlowStore((s) => s.nodes);
  const enabled = useFlowStore((s) => s.branchBandsEnabled);
  if (!enabled) return null;
  const bands = computeBranchBands(nodes);
  if (bands.length === 0) return null;

  return (
    <ViewportPortal>
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
              border: `1px solid ${mix(b.colorVar, nested ? 55 : 38)}`,
              borderLeft: `3px solid ${mix(b.colorVar, 70)}`,
              borderRadius: 9,
              pointerEvents: 'none',
              zIndex: -1,
            }}
          >
            <span style={{
              position: 'absolute', top: 0, left: 0,
              font: '800 9px/1.4 system-ui, sans-serif', letterSpacing: '0.08em',
              padding: '2px 10px', borderRadius: '9px 0 8px 0',
              color: 'oklch(17% 0.02 275)',
              background: nested ? `color-mix(in oklch, ${b.colorVar}, white 14%)` : b.colorVar,
            }}>
              {branchPillLabel(b.branchKey)}
            </span>
          </div>
        );
      })}
    </ViewportPortal>
  );
}

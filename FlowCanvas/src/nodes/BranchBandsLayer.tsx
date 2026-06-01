// FlowCanvas/src/nodes/BranchBandsLayer.tsx
import { ViewportPortal } from '@xyflow/react';
import { useFlowStore } from '../stores/useFlowStore';
import { computeBranchBands } from '../utils/branchBands';
import { mix } from '../utils/tokens';

export default function BranchBandsLayer() {
  const nodes = useFlowStore((s) => s.nodes);
  const enabled = useFlowStore((s) => s.branchBandsEnabled);
  if (!enabled) return null;
  const bands = computeBranchBands(nodes);
  if (bands.length === 0) return null;

  return (
    <ViewportPortal>
      {bands.map((b) => (
        <div
          key={b.id}
          data-testid="branch-band"
          data-branch={b.branchKey}
          style={{
            position: 'absolute',
            transform: `translate(${b.x}px, ${b.y}px)`,
            width: b.width,
            height: b.height,
            background: mix(b.colorVar, 8),
            borderLeft: `3px solid ${b.colorVar}`,
            borderRadius: 8,
            pointerEvents: 'none',
            zIndex: -1, // behind .react-flow__node
          }}
        />
      ))}
    </ViewportPortal>
  );
}

import { memo } from 'react';
import { BaseEdge, getSmoothStepPath, type EdgeProps } from '@xyflow/react';
import { mix } from '../utils/tokens';
import { markerIdForStroke } from './EdgeMarkers';
import { useFlowStore } from '../stores/useFlowStore';
import './animatededge.css';

function AnimatedEdge(props: EdgeProps) {
  const { id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, source, style } = props;

  const isRunning = useFlowStore((s) => s.isRunning);
  const blockStates = useFlowStore((s) => s.blockStates);
  const reducedMotion = useFlowStore((s) => s.reducedMotion);

  const [edgePath] = getSmoothStepPath({
    sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, borderRadius: 8,
  });

  // Color comes from the edge's style.stroke (set by getBranchVisual / defaultEdgeOptions /
  // selection). Branch edges = --fc-branch-*, continuation = --fc-accent, plain = --fc-edge-idle.
  const color = (typeof style?.stroke === 'string' ? style.stroke : undefined) ?? 'var(--fc-edge-idle)';
  const markerId = markerIdForStroke(color);

  const sourceState = blockStates.get(source);
  const active = isRunning && (sourceState === 'success' || sourceState === 'running');

  const gradientId = `fc-grad-${id}`;
  const strokeWidth = typeof style?.strokeWidth === 'number' ? style.strokeWidth : active ? 2.5 : 2;

  return (
    <>
      <defs>
        {/* userSpaceOnUse so the gradient orients along the actual edge; dim→full toward target. */}
        <linearGradient id={gradientId} gradientUnits="userSpaceOnUse" x1={sourceX} y1={sourceY} x2={targetX} y2={targetY}>
          <stop offset="0%" stopColor={mix(color, 30)} />
          <stop offset="100%" stopColor={color} />
        </linearGradient>
      </defs>
      <BaseEdge
        id={id}
        path={edgePath}
        markerEnd={`url(#${markerId})`}
        style={{ ...style, stroke: `url(#${gradientId})`, strokeWidth }}
      />
      {active && !reducedMotion && (
        <circle
          className="fc-edge-packet"
          r={4}
          cx={0}
          cy={0}
          fill="var(--fc-edge-packet)"
          filter="url(#fc-packet-glow)"
          style={{ offsetPath: `path('${edgePath}')` }}
        />
      )}
    </>
  );
}

export default memo(AnimatedEdge);

import { memo } from 'react';
import { BaseEdge, getSmoothStepPath, getStraightPath, type EdgeProps } from '@xyflow/react';
import { mix } from '../utils/tokens';
import { markerIdForStroke } from './EdgeMarkers';
import { useFlowStore } from '../stores/useFlowStore';
import { selectEdgePathStatus } from '../stores/selectors/edgePath';
import './animatededge.css';

function AnimatedEdge(props: EdgeProps) {
  const { id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, source, style } = props;

  const isRunning = useFlowStore((s) => s.isRunning);
  const blockStates = useFlowStore((s) => s.blockStates);
  const reducedMotion = useFlowStore((s) => s.reducedMotion);
  // Path overlay status. Returns a string → referentially stable, no extra renders.
  const pathStatus = useFlowStore((s) => selectEdgePathStatus(s, id));

  // Geometry (not data.branchPath / sourceHandle) is the discriminator: imported branch edges
  // carry no branchPath, so metadata would misclassify them. Aligned, downward edges (the
  // continuation spine) get a literal straight line so the run packet glides cleanly; X-offset
  // edges (branch/loop corridors — IF "false", container "continue", branch-first) keep
  // smoothstep so they route orthogonally around child blocks. See design doc.
  // ALIGN_EPS: flow coords are integers, so centered equal-width handles compute dx==0 exactly;
  // 0.5 absorbs sub-pixel float drift and never catches a real corridor (smallest offset ~70px).
  const ALIGN_EPS = 0.5;
  const isSpine = Math.abs(sourceX - targetX) < ALIGN_EPS && targetY > sourceY;
  const [edgePath] = isSpine
    ? getStraightPath({ sourceX, sourceY, targetX, targetY })
    : getSmoothStepPath({
        sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, borderRadius: 8,
      });

  // Color comes from the edge's style.stroke (set by getBranchVisual / defaultEdgeOptions).
  // Branch edges = --fc-branch-*, continuation = --fc-accent, plain = --fc-edge-idle.
  const color = (typeof style?.stroke === 'string' ? style.stroke : undefined) ?? 'var(--fc-edge-idle)';
  const markerId = markerIdForStroke(color);

  const sourceState = blockStates.get(source);
  const active = isRunning && (sourceState === 'success' || sourceState === 'running');

  const gradientId = `fc-grad-${id}`;

  // ── Execution-path overlay (persists after the run; decoupled from isRunning) ──
  // on-path: full-strength stroke + soft glow. Idle-grey spine edges promote to the traversed
  //   token so a traveled wire actually reads as lit; branch edges keep their branch color.
  // untaken: a branch that did not fire — faded via the .fc-edge-untaken class.
  const onPath = pathStatus === 'on-path';
  const untaken = pathStatus === 'untaken';
  const onPathStroke = color === 'var(--fc-edge-idle)' ? 'var(--fc-edge-traversed)' : color;

  let stroke: string;
  let strokeWidth: number;
  let edgeClass: string | undefined;
  if (onPath) {
    stroke = onPathStroke;
    strokeWidth = typeof style?.strokeWidth === 'number' ? style.strokeWidth : 3;
    edgeClass = 'fc-edge-onpath';
  } else if (untaken) {
    stroke = color;
    strokeWidth = typeof style?.strokeWidth === 'number' ? style.strokeWidth : 1.5;
    edgeClass = 'fc-edge-untaken';
  } else {
    // Idle: existing behavior — dim→full gradient, widening while the source is active.
    stroke = `url(#${gradientId})`;
    strokeWidth = typeof style?.strokeWidth === 'number' ? style.strokeWidth : active ? 2.5 : 2;
    edgeClass = undefined;
  }

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
        className={edgeClass}
        path={edgePath}
        markerEnd={`url(#${markerId})`}
        style={{ ...style, stroke, strokeWidth }}
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

import { memo, type CSSProperties } from 'react';
import { BaseEdge, getSmoothStepPath, getStraightPath, type EdgeProps } from '@xyflow/react';
import { mix } from '../utils/tokens';
import { markerIdForStroke } from './EdgeMarkers';
import { useFlowStore } from '../stores/useFlowStore';
import { selectEdgePathStatus, selectEdgeIsBranch } from '../stores/selectors/edgePath';
import './animatededge.css';

function AnimatedEdge(props: EdgeProps) {
  const { id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, source, style } = props;

  const isRunning = useFlowStore((s) => s.isRunning);
  const blockStates = useFlowStore((s) => s.blockStates);
  const reducedMotion = useFlowStore((s) => s.reducedMotion);
  // Path overlay status. Returns a string → referentially stable, no extra renders.
  const pathStatus = useFlowStore((s) => selectEdgePathStatus(s, id));
  // Branch arm (keeps its own hue) vs spine/plain/continuation (promotes to traversed cyan).
  const isBranch = useFlowStore((s) => selectEdgeIsBranch(s, id));

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
  // on-path = a neon-halo wire: a bright, saturated core stroke wrapped in a 3-layer colored
  //   bloom (.fc-edge-onpath). The bloom reads --fc-onpath, so each lit edge glows in its own
  //   hue — idle-grey spine edges promote to the cyan traversed token; branch edges keep their
  //   branch color (then=green, catch/else=red). untaken: a branch that did not fire, faded.
  const onPath = pathStatus === 'on-path';
  const untaken = pathStatus === 'untaken';
  // The lit hue: branch arms keep their own color; every spine/plain/continuation edge promotes
  // to the traversed cyan. Keyed on the STRUCTURAL branch test, not the stroke color — imported
  // preset edges carry literal grey hex (#555/#666 from FlowCanvasBridge), not the idle token, so
  // a color check left them grey and `color-mix(... white)` washed them to near-white spines.
  // Drives both the brightened core stroke and the colored bloom (via --fc-onpath below).
  const onPathHue = isBranch ? color : 'var(--fc-edge-traversed)';

  let stroke: string;
  let strokeWidth: number;
  let edgeClass: string | undefined;
  if (onPath) {
    // Bright, saturated core (a slight lift toward white — keep it well short of washing the
    // hue out, or low-chroma cyan spines read as grey); the colored bloom in CSS does the glow.
    stroke = `color-mix(in oklch, ${onPathHue}, white 30%)`;
    strokeWidth = typeof style?.strokeWidth === 'number' ? style.strokeWidth : 2.5;
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

  // The bloom color tracks the lit hue through this custom property (consumed by .fc-edge-onpath).
  const edgeStyle: CSSProperties = { ...style, stroke, strokeWidth };
  if (onPath) (edgeStyle as Record<string, string>)['--fc-onpath'] = onPathHue;

  // On the lit path the arrowhead matches the glow hue via a per-edge marker — the shared token
  // markers have no traversed/cyan variant and can't match imported hex strokes, so a per-edge
  // marker (like the gradient above) is the only thing that colors every lit tip correctly.
  // Off-path edges keep their shared token marker.
  const onPathMarkerId = `fc-arrow-onpath-${id}`;
  const markerEnd = onPath ? `url(#${onPathMarkerId})` : `url(#${markerId})`;

  return (
    <>
      <defs>
        {/* userSpaceOnUse so the gradient orients along the actual edge; dim→full toward target. */}
        <linearGradient id={gradientId} gradientUnits="userSpaceOnUse" x1={sourceX} y1={sourceY} x2={targetX} y2={targetY}>
          <stop offset="0%" stopColor={mix(color, 30)} />
          <stop offset="100%" stopColor={color} />
        </linearGradient>
        {onPath && (
          <marker
            id={onPathMarkerId}
            viewBox="0 0 10 10"
            refX="9"
            refY="5"
            markerWidth="7"
            markerHeight="7"
            orient="auto-start-reverse"
          >
            <path d="M0 0 L10 5 L0 10 z" fill={onPathHue} />
          </marker>
        )}
      </defs>
      <BaseEdge
        id={id}
        className={edgeClass}
        path={edgePath}
        markerEnd={markerEnd}
        style={edgeStyle}
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

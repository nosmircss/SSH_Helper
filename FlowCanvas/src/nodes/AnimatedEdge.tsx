import { memo } from 'react';
import { BaseEdge, getSmoothStepPath, type EdgeProps } from '@xyflow/react';
import { useFlowStore } from '../stores/useFlowStore';

const stateColors: Record<string, string> = {
  success: 'var(--fc-state-success)',
  running: 'var(--fc-accent)',
  error: 'var(--fc-state-error)',
};

function AnimatedEdge(props: EdgeProps) {
  const {
    id,
    sourceX,
    sourceY,
    targetX,
    targetY,
    sourcePosition,
    targetPosition,
    source,
    style,
    markerEnd,
  } = props;

  const isRunning = useFlowStore((s) => s.isRunning);
  const blockStates = useFlowStore((s) => s.blockStates);

  const [edgePath] = getSmoothStepPath({
    sourceX,
    sourceY,
    targetX,
    targetY,
    sourcePosition,
    targetPosition,
    borderRadius: 8,
  });

  const sourceState = blockStates.get(source);
  const shouldAnimate = isRunning && (sourceState === 'success' || sourceState === 'running');
  const strokeColor = shouldAnimate
    ? stateColors[sourceState!] || 'var(--fc-edge-idle)'
    : 'var(--fc-edge-idle)';

  if (shouldAnimate) {
    return (
      <>
        {/* Base edge (solid, dimmed) */}
        <BaseEdge
          id={`${id}-base`}
          path={edgePath}
          markerEnd={markerEnd}
          style={{
            ...style,
            stroke: strokeColor,
            strokeWidth: 2,
            opacity: 0.3,
          }}
        />
        {/* Animated overlay (marching ants) */}
        <BaseEdge
          id={id}
          path={edgePath}
          markerEnd={markerEnd}
          style={{
            ...style,
            stroke: strokeColor,
            strokeWidth: 2,
            strokeDasharray: '8 4',
            animation: 'marchingAnts 0.5s linear infinite',
          }}
        />
        <style>{`
          @keyframes marchingAnts {
            to {
              stroke-dashoffset: -12;
            }
          }
        `}</style>
      </>
    );
  }

  return (
    <BaseEdge
      id={id}
      path={edgePath}
      markerEnd={markerEnd}
      style={{
        ...style,
        stroke: 'var(--fc-edge-idle)',
        strokeWidth: 2,
      }}
    />
  );
}

export default memo(AnimatedEdge);

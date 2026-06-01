// FlowCanvas/src/nodes/EdgeMarkers.tsx
// Tokenized arrowhead markers + the shared packet glow filter for Live Wires (Wave 2b).
// Rendered once (hidden) in App; url(#id) marker refs resolve document-wide. Every fill is a
// var(--fc-*) token (Decision #4 gate-safe). markerIdForStroke maps an edge's resolved
// style.stroke token to its marker id so the arrowhead matches the edge color.
import { type JSX } from 'react';

const EDGE_MARKERS = [
  { key: 'then', colorVar: 'var(--fc-branch-then)' },
  { key: 'else', colorVar: 'var(--fc-branch-else)' },
  { key: 'elif', colorVar: 'var(--fc-branch-elif)' },
  { key: 'do', colorVar: 'var(--fc-branch-do)' },
  { key: 'try', colorVar: 'var(--fc-branch-try)' },
  { key: 'catch', colorVar: 'var(--fc-branch-catch)' },
  { key: 'finally', colorVar: 'var(--fc-branch-finally)' },
  { key: 'case', colorVar: 'var(--fc-branch-case)' },
  { key: 'default', colorVar: 'var(--fc-branch-default)' },
  { key: 'parallel', colorVar: 'var(--fc-branch-parallel)' },
  { key: 'fallback', colorVar: 'var(--fc-branch-fallback)' },
  { key: 'idle', colorVar: 'var(--fc-edge-idle)' },
  { key: 'accent', colorVar: 'var(--fc-accent)' },
] as const;

/** Map an edge's resolved style.stroke (a var(--fc-*) token) to its arrowhead marker id. */
export function markerIdForStroke(stroke: string | undefined): string {
  const found = EDGE_MARKERS.find((m) => m.colorVar === stroke);
  return `fc-arrow-${found ? found.key : 'idle'}`;
}

export function EdgeMarkers(): JSX.Element {
  return (
    <svg width="0" height="0" aria-hidden="true" style={{ position: 'absolute', overflow: 'hidden' }}>
      <defs>
        {EDGE_MARKERS.map(({ key, colorVar }) => (
          <marker
            key={key}
            id={`fc-arrow-${key}`}
            viewBox="0 0 10 10"
            refX="9"
            refY="5"
            markerWidth="7"
            markerHeight="7"
            orient="auto-start-reverse"
          >
            <path d="M0 0 L10 5 L0 10 z" fill={colorVar} />
          </marker>
        ))}
        <filter id="fc-packet-glow" x="-150%" y="-150%" width="400%" height="400%">
          <feGaussianBlur stdDeviation="2" result="b" />
          <feMerge>
            <feMergeNode in="b" />
            <feMergeNode in="SourceGraphic" />
          </feMerge>
        </filter>
      </defs>
    </svg>
  );
}

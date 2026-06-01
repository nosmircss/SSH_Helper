import { mix } from './tokens';

export type NodeExecState = 'idle' | 'running' | 'success' | 'error' | 'skipped' | 'disabled';

/**
 * Balanced idle "neon ring": a crisp 1px structural ring + a softened ambient glow + a faint
 * inner light, all derived from the block's category border hue via color-mix. Alphas 36/46/60
 * are the approved "Balanced" intensity (below running's 0.4a so idle never out-shouts a run).
 */
export function idleNeon(border: string): string {
  return (
    `0 0 0 1px ${mix(border, 36)}, ` +
    `0 0 10px -2px ${mix(border, 46)}, ` +
    `inset 0 0 10px -7px ${mix(border, 60)}`
  );
}

/** Card border color: white when selected, muted when disabled, else the persistent category hue. */
export function nodeBorderColor(opts: { selected: boolean; isDisabled: boolean; border: string }): string {
  if (opts.selected) return 'var(--fc-border-selected)';
  if (opts.isDisabled) return 'var(--fc-border-muted)';
  return opts.border;
}

/**
 * Inline box-shadow for the card BEFORE the heat-ring wrap (kept in BaseBlock). Mirrors the
 * historical success/skipped/selected ladder and adds the idle category ring as the terminal
 * branch, replacing the old `'none'`. running/error/disabled return 'none' — running & error are
 * owned by CSS class animations (which paint after inline styles); the idle ring is suppressed
 * when the heat ring is active so they never double-ring.
 */
export function resolveNodeShadow(opts: {
  execState: NodeExecState;
  selected: boolean;
  heatActive: boolean;
  border: string;
}): string {
  const { execState, selected, heatActive, border } = opts;
  if (execState === 'success') return '0 0 10px var(--fc-glow-success)';
  if (execState === 'skipped') return '0 0 16px var(--fc-glow-skipped)';
  if (selected) return '0 0 12px var(--fc-glow-selected)';
  if (execState === 'idle' && !heatActive) return idleNeon(border);
  return 'none';
}

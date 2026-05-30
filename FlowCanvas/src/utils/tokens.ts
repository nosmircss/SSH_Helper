// FlowCanvas/src/utils/tokens.ts
import type { BlockCategory } from '../blockDefs/registry';

/** Authored default for a new comment. Serialized to layout-autosave JSON and to C# CanvasComment.Color,
 *  so this MUST stay byte-identical to the C# default and MUST NOT be routed through a CSS var. */
export const DEFAULT_COMMENT_COLOR = '#e0c040';

export interface CategoryVarSet {
  border: string;
  bg: string;
  badge: string;
  badgeText: string;
  text: string;
  icon: string;
}

/** Category colors as CSS var() strings. CSS custom properties resolve inside React inline styles
 *  and inside SVG fill, so this is the single source consumed by BaseBlock and the minimap. */
export const CATEGORY_VARS: Record<BlockCategory, CategoryVarSet> = {
  ssh: catVars('ssh'),
  'control-flow': catVars('control-flow'),
  data: catVars('data'),
  network: catVars('network'),
  io: catVars('io'),
  grid: catVars('grid'),
  timing: catVars('timing'),
};

function catVars(c: string): CategoryVarSet {
  return {
    border: `var(--fc-cat-${c}-border)`,
    bg: `var(--fc-cat-${c}-bg)`,
    badge: `var(--fc-cat-${c}-badge)`,
    badgeText: `var(--fc-cat-${c}-badge-text)`,
    text: `var(--fc-cat-${c}-text)`,
    icon: `var(--fc-cat-${c}-icon)`,
  };
}

/** Apply an alpha to a `var(--fc-*)` color via color-mix over transparent. Replaces the old
 *  `color + '<hex-alpha>'` idiom, which produced invalid CSS (e.g. `var(--fc-accent)55`) once the
 *  category colors became var() strings instead of raw hex. color-mix(in oklch, …) is supported by
 *  the WebView2 Chromium runtime. `pct` is the opacity percentage (0–100). */
export function mix(color: string, pct: number): string {
  return `color-mix(in oklch, ${color} ${pct}%, transparent)`;
}

/** Resolve a `var(--fc-*)` reference (or bare token name) to a concrete color.
 *  Used only where a raw color string is required (SVG MiniMap nodeColor/maskColor). */
export function resolveCssVar(varRef: string, fallback = '#4a9eff'): string {
  if (typeof window === 'undefined') return fallback;
  const name = varRef.replace(/^var\(\s*/, '').replace(/\s*\)$/, '').split(',')[0].trim();
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return value || fallback;
}

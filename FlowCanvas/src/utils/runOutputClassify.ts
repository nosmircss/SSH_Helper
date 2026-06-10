export type RunOutputLineKind = 'banner' | 'error' | 'normal';

// Form1's host/script delimiters look like: "############### CONNECTED TO ... ###############"
const BANNER_RE = /^#{6,}.*#{6,}$/;

// Conservative, best-effort error heuristic. Cosmetic only — the Color toggle is the escape hatch.
const ERROR_RE = /(command (parse )?error|command fail|return code\s+-?\d|%\s*invalid|permission denied|\bfail(?:ed)?\b)/i;

export function classifyRunOutputLine(line: string): RunOutputLineKind {
  const trimmed = line.trim();
  if (BANNER_RE.test(trimmed)) return 'banner';
  if (ERROR_RE.test(line)) return 'error';
  return 'normal';
}

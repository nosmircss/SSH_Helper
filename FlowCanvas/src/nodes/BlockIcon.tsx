// FlowCanvas/src/nodes/BlockIcon.tsx
// Vendored Lucide stroke icons (Decision #9): inlined as JSX so they bundle into the offline
// WebView2 build with zero runtime network, zero font, zero npm dependency. Every glyph uses
// stroke="currentColor" (Decision #4 gate-safe — the category tint is inherited from the parent's
// `color`). Keyed by the registry `def.icon` string (~40 distinct values); unknown keys fall back
// to a neutral `box` glyph so Unknown-type and any future icon key render without throwing.
import { type JSX } from 'react';

/** Children (the <path>/<circle>/… stroke geometry) for each icon key. The wrapping <svg> with
 *  size + stroke attrs is applied once in BlockIcon, so each entry is geometry-only. */
const ICONS: Record<string, JSX.Element> = {
  // SSH category
  ssh: (<><polyline points="4 17 10 11 4 5" /><line x1="12" y1="19" x2="20" y2="19" /></>), // SquareTerminal-ish prompt
  term: (<><rect x="3" y="4" width="18" height="16" rx="2" /><polyline points="7 9 10 12 7 15" /><line x1="13" y1="15" x2="17" y2="15" /></>), // TerminalSquare
  sftp: (<><path d="M12 3v12" /><polyline points="7 8 12 3 17 8" /><path d="M5 21h14" /></>), // FileUp / upload arrow
  // Control flow
  if: (<><line x1="6" y1="3" x2="6" y2="15" /><circle cx="18" cy="6" r="3" /><circle cx="6" cy="18" r="3" /><path d="M18 9a9 9 0 0 1-9 9" /></>), // GitBranch
  for: (<><path d="m17 2 4 4-4 4" /><path d="M3 11v-1a4 4 0 0 1 4-4h14" /><path d="m7 22-4-4 4-4" /><path d="M21 13v1a4 4 0 0 1-4 4H3" /></>), // Repeat
  while: (<><path d="M21 12a9 9 0 1 1-3-6.7" /><polyline points="21 3 21 9 15 9" /></>), // RotateCw
  switch: (<><circle cx="12" cy="18" r="3" /><circle cx="6" cy="6" r="3" /><circle cx="18" cy="6" r="3" /><path d="M18 9v2a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2V9" /><path d="M12 12v3" /></>), // GitFork
  parallel: (<><rect x="3" y="4" width="18" height="4" rx="1" /><rect x="3" y="10" width="18" height="4" rx="1" /><rect x="3" y="16" width="18" height="4" rx="1" /></>), // Rows3
  try: (<><path d="M20 13c0 5-3.5 7.5-7.7 8.95a1 1 0 0 1-.6 0C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.2-2.7a1 1 0 0 1 1.6 0C15.5 3.8 18 5 20 5a1 1 0 0 1 1 1z" /><path d="M12 8v4" /><path d="M12 16h.01" /></>), // ShieldAlert
  break: (<><circle cx="12" cy="12" r="9" /><rect x="9" y="9" width="6" height="6" rx="1" /></>), // CircleStop
  continue: (<><polygon points="5 4 15 12 5 20 5 4" /><line x1="19" y1="5" x2="19" y2="19" /></>), // SkipForward
  call: (<><polyline points="9 10 4 15 9 20" /><path d="M20 4v7a4 4 0 0 1-4 4H4" /></>), // CornerDownLeft-style call
  return: (<><polyline points="9 14 4 9 9 4" /><path d="M20 20v-7a4 4 0 0 0-4-4H4" /></>), // CornerUpLeft
  exit: (<><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" /><polyline points="16 17 21 12 16 7" /><line x1="21" y1="12" x2="9" y2="12" /></>), // LogOut
  // Data
  extract: (<><circle cx="6" cy="6" r="3" /><circle cx="6" cy="18" r="3" /><line x1="20" y1="4" x2="8.12" y2="15.88" /><line x1="14.47" y1="14.48" x2="20" y2="20" /><line x1="8.12" y1="8.12" x2="12" y2="12" /></>), // Scissors
  set: (<><line x1="5" y1="9" x2="19" y2="9" /><line x1="5" y1="15" x2="19" y2="15" /></>), // Equal / assignment
  parse: (<><path d="M8 3H7a2 2 0 0 0-2 2v5a2 2 0 0 1-2 2 2 2 0 0 1 2 2v5a2 2 0 0 0 2 2h1" /><path d="M16 3h1a2 2 0 0 1 2 2v5a2 2 0 0 0 2 2 2 2 0 0 0-2 2v5a2 2 0 0 1-2 2h-1" /></>), // Braces
  table: (<><rect x="3" y="3" width="18" height="18" rx="2" /><line x1="3" y1="9" x2="21" y2="9" /><line x1="3" y1="15" x2="21" y2="15" /><line x1="9" y1="3" x2="9" y2="21" /></>), // Table
  assert: (<><circle cx="12" cy="12" r="9" /><polyline points="8.5 12.5 11 15 16 9" /></>), // CircleCheck
  // Network
  ping: (<><path d="M22 12h-4l-3 9L9 3l-3 9H2" /></>), // Activity
  dns: (<><circle cx="12" cy="12" r="9" /><path d="M2 12h20" /><path d="M12 3a14 14 0 0 1 0 18 14 14 0 0 1 0-18z" /></>), // Globe
  port: (<><path d="m13 2-3 7h5l-3 7" /><path d="M5 12H3" /><path d="M21 12h-2" /></>), // PlugZap / EthernetPort-ish
  http: (<><circle cx="12" cy="12" r="9" /><path d="M2 12h20" /><path d="M12 3a14 14 0 0 1 0 18 14 14 0 0 1 0-18z" /><path d="m16 8 2 2-2 2" /></>), // Globe2 with arrow
  webhook: (<><path d="M18 16.98h-5.99c-1.1 0-1.95.94-2.48 1.9A4 4 0 0 1 2 17c.01-.7.2-1.4.57-2" /><path d="m6 17 3.13-5.78c.53-.97.1-2.18-.5-3.1a4 4 0 1 1 6.89-4.06" /><path d="m12 6 3.13 5.73C15.66 12.7 16.9 13 18 13a4 4 0 0 1 0 8" /></>), // Webhook
  oauth: (<><circle cx="8" cy="15" r="4" /><path d="M10.85 12.15 19 4" /><path d="m18 5 2 2" /><path d="m15 8 2 2" /></>), // KeyRound
  vault: (<><rect x="3" y="11" width="18" height="11" rx="2" /><path d="M7 11V7a5 5 0 0 1 10 0v4" /></>), // Lock
  // IO
  print: (<><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" /></>), // MessageSquare
  input: (<><path d="M5 4h1a3 3 0 0 1 3 3 3 3 0 0 1 3-3h1" /><path d="M13 20h-1a3 3 0 0 1-3-3 3 3 0 0 1-3 3H5" /><path d="M5 16H4a2 2 0 0 1-2-2v-4a2 2 0 0 1 2-2h1" /><path d="M13 8h7a2 2 0 0 1 2 2v4a2 2 0 0 1-2 2h-7" /><path d="M9 7v10" /></>), // TextCursorInput
  choose: (<><path d="m3 17 2 2 4-4" /><path d="m3 7 2 2 4-4" /><line x1="13" y1="6" x2="21" y2="6" /><line x1="13" y1="12" x2="21" y2="12" /><line x1="13" y1="18" x2="21" y2="18" /></>), // ListChecks
  multi: (<><path d="m3 17 2 2 4-4" /><path d="m3 7 2 2 4-4" /><line x1="13" y1="6" x2="21" y2="6" /><line x1="13" y1="12" x2="21" y2="12" /><line x1="13" y1="18" x2="21" y2="18" /></>), // ListChecks (multi)
  confirm: (<><circle cx="12" cy="12" r="9" /><path d="M9.5 9a2.5 2.5 0 0 1 4.5 1.5c0 1.5-2 2-2 3" /><path d="M12 17h.01" /></>), // CircleHelp
  read: (<><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><polyline points="14 2 14 8 20 8" /><line x1="8" y1="13" x2="16" y2="13" /><line x1="8" y1="17" x2="13" y2="17" /></>), // FileText
  write: (<><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><polyline points="14 2 14 8 20 8" /><path d="m10 18 3-3-2-2-3 3v2z" /></>), // FileOutput / FilePen
  exists: (<><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h6" /><polyline points="14 2 14 8 20 8" /><circle cx="16" cy="16" r="3" /><line x1="20.5" y1="20.5" x2="18.1" y2="18.1" /></>), // FileSearch
  audio: (<><polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5" /><path d="M15.54 8.46a5 5 0 0 1 0 7.07" /><path d="M19.07 4.93a10 10 0 0 1 0 14.14" /></>), // Volume2
  log: (<><path d="M8 3H7a2 2 0 0 0-2 2v15a1 1 0 0 0 1 1h13a2 2 0 0 0 2-2V8z" /><path d="M16 3v4a1 1 0 0 0 1 1h4" /><line x1="9" y1="13" x2="15" y2="13" /><line x1="9" y1="17" x2="15" y2="17" /></>), // ScrollText
  notify: (<><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" /><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0" /></>), // Bell
  terminal: (<><rect x="3" y="4" width="18" height="16" rx="2" /><polyline points="7 9 10 12 7 15" /><line x1="13" y1="15" x2="17" y2="15" /></>), // SquareTerminal
  // Grid
  column: (<><rect x="3" y="3" width="18" height="18" rx="2" /><line x1="9" y1="3" x2="9" y2="21" /><line x1="15" y1="3" x2="15" y2="21" /></>), // Columns
  env: (<><path d="M20 7h-9" /><path d="M14 17H5" /><circle cx="17" cy="17" r="3" /><circle cx="7" cy="7" r="3" /></>), // Settings2-ish sliders
  // Timing
  wait: (<><circle cx="12" cy="12" r="9" /><polyline points="12 7 12 12 15 14" /></>), // Clock
  // Neutral fallback
  box: (<><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z" /><path d="m3.3 7 8.7 5 8.7-5" /><path d="M12 22V12" /></>), // Boxes / box
};

export function BlockIcon({ name, size = 14 }: { name: string; size?: number }): JSX.Element {
  const geometry = ICONS[name] ?? ICONS.box;
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.75}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      style={{ flexShrink: 0, display: 'block' }}
    >
      {geometry}
    </svg>
  );
}

/**
 * Run Output console — a live mirror of the main form's output box.
 * Renders the executionSlice.runOutput buffer with optional light styling
 * (teal banners, red error lines) gated behind the Color toggle.
 */
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { classifyRunOutputLine } from '../utils/runOutputClassify';

const KIND_COLOR: Record<string, string> = {
  banner: 'var(--fc-host-accent)',
  error: 'var(--fc-state-error)',
  normal: 'var(--fc-term-text)',
  plain: 'var(--fc-term-text)', // color toggle off — no per-line classification
};

export default function RunOutputView() {
  const runOutput = useFlowStore((s) => s.runOutput);
  const isRunning = useFlowStore((s) => s.isRunning);
  const color = useFlowStore((s) => s.runOutputColor);
  const wrap = useFlowStore((s) => s.runOutputWrap);
  const follow = useFlowStore((s) => s.runOutputFollow);
  const toggleColor = useFlowStore((s) => s.toggleRunOutputColor);
  const toggleWrap = useFlowStore((s) => s.toggleRunOutputWrap);
  const toggleFollow = useFlowStore((s) => s.toggleRunOutputFollow);
  const togglePoppedOut = useFlowStore((s) => s.toggleRunOutputPoppedOut);
  const poppedOut = useFlowStore((s) => s.runOutputPoppedOut);

  const [findOpen, setFindOpen] = useState(false);
  const [findQuery, setFindQuery] = useState('');

  const matchCount = useMemo(() => {
    if (!findQuery) return 0;
    const q = findQuery.toLowerCase();
    let count = 0, idx = 0;
    const hay = runOutput.toLowerCase();
    while ((idx = hay.indexOf(q, idx)) !== -1) { count++; idx += q.length; }
    return count;
  }, [findQuery, runOutput]);

  const scrollRef = useRef<HTMLDivElement>(null);

  // Split into lines and strip the trailing CR of each CRLF pair. The \r is a line-ending
  // artifact (the main form's TextBox doesn't show it either); leaving it on causes a
  // CR-only blank line ("\r") to collapse in a white-space:pre div, dropping blank lines.
  const lines = useMemo(() => runOutput.split('\n').map((l) => l.replace(/\r$/, '')), [runOutput]);

  // Stick-to-bottom while following.
  useEffect(() => {
    if (follow && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [runOutput, follow]);

  // If the user scrolls up, stop following (re-enable via the Follow button).
  const onScroll = () => {
    const el = scrollRef.current;
    if (!el || !follow) return;
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 24;
    if (!atBottom) toggleFollow();
  };

  const hasOutput = runOutput.length > 0;
  const whiteSpace = wrap ? 'pre-wrap' : 'pre';

  return (
    <div
      data-testid="run-output-view"
      style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, background: 'var(--fc-term-bg)' }}
    >
      {/* Toolbar */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 2, padding: '0 8px', height: 26,
        background: 'var(--fc-term-surface)', borderBottom: '1px solid var(--fc-border)', flexShrink: 0,
      }}>
        {isRunning && (
          <span data-testid="run-output-live" style={{ display: 'flex', alignItems: 'center', gap: 5, marginRight: 8, fontSize: 10, fontWeight: 600, color: 'var(--fc-state-success)' }}>
            <span style={{ width: 7, height: 7, borderRadius: '50%', background: 'var(--fc-state-success)', boxShadow: '0 0 6px var(--fc-state-success)' }} />
            LIVE
          </span>
        )}
        <div style={{ flex: 1 }} />
        <ToolbarButton testid="run-output-btn-find" active={findOpen} onClick={() => { if (findOpen) setFindQuery(''); setFindOpen((v) => !v); }} title="Find">⌕ Find</ToolbarButton>
        <ToolbarButton testid="run-output-btn-follow" active={follow} onClick={toggleFollow} title="Stick to bottom">⤓ Follow</ToolbarButton>
        <ToolbarButton testid="run-output-btn-wrap" active={wrap} onClick={toggleWrap} title="Word wrap">↵ Wrap</ToolbarButton>
        <ToolbarButton testid="run-output-btn-color" active={color} onClick={toggleColor} title="Colorize output">🎨 Color</ToolbarButton>
        <ToolbarButton testid="run-output-btn-copy" active={false} onClick={() => navigator.clipboard.writeText(runOutput)} title="Copy all">⧉ Copy</ToolbarButton>
        {/* When popped out, the overlay's own Dock button replaces this — hide it to avoid redundancy. */}
        {!poppedOut && (
          <ToolbarButton testid="run-output-btn-popout" active={false} onClick={togglePoppedOut} title="Pop out">⤢ Pop out</ToolbarButton>
        )}
      </div>

      {findOpen && (
        <div style={{
          display: 'flex', alignItems: 'center', gap: 8, padding: '4px 8px', flexShrink: 0,
          background: 'var(--fc-term-surface)', borderBottom: '1px solid var(--fc-border)',
        }}>
          <input
            data-testid="run-output-find-input"
            autoFocus
            value={findQuery}
            onChange={(e) => setFindQuery(e.target.value)}
            placeholder="Find in output…"
            style={{
              flex: 1, background: 'var(--fc-term-bg)', color: 'var(--fc-term-text)',
              border: '1px solid var(--fc-border)', borderRadius: 4, padding: '3px 6px',
              fontFamily: 'var(--fc-font-mono)', fontSize: 11,
            }}
          />
          <span data-testid="run-output-find-count" style={{ fontSize: 10, color: 'var(--fc-text-muted)' }}>
            {findQuery ? `${matchCount} match${matchCount === 1 ? '' : 'es'}` : ''}
          </span>
        </div>
      )}

      {/* Body */}
      <div
        ref={scrollRef}
        onScroll={onScroll}
        style={{ flex: 1, overflow: 'auto', padding: 8, fontFamily: 'var(--fc-font-mono)', fontSize: 11, lineHeight: 1.5 }}
      >
        {!hasOutput && (
          <div style={{ color: 'var(--fc-text-muted)' }}>No run output yet — run a script to see it here.</div>
        )}
        {hasOutput && (
          <div>
            {lines.map((line, i) => {
              // color off -> 'plain' (no classification colors); find highlight still applies in both modes.
              const kind = color ? classifyRunOutputLine(line) : 'plain';
              return (
                <div key={i} data-testid="run-output-line" data-kind={kind} style={{ color: KIND_COLOR[kind], whiteSpace }}>
                  {highlight(line, findQuery)}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

function ToolbarButton({ testid, active, onClick, title, children }: {
  testid: string; active: boolean; onClick: () => void; title: string; children: ReactNode;
}) {
  return (
    <button
      data-testid={testid}
      onClick={onClick}
      title={title}
      style={{
        fontSize: 11, fontWeight: 600, padding: '4px 7px', borderRadius: 5, cursor: 'pointer',
        background: active ? 'var(--fc-accent-surface)' : 'transparent',
        color: active ? 'var(--fc-accent)' : 'var(--fc-text-muted)',
        border: `1px solid ${active ? 'var(--fc-border-selected)' : 'transparent'}`,
        fontFamily: 'inherit',
      }}
    >
      {children}
    </button>
  );
}

function highlight(line: string, query: string): ReactNode {
  if (!query) return line || ' ';
  const q = query.toLowerCase();
  const lower = line.toLowerCase();
  const parts: ReactNode[] = [];
  let i = 0, key = 0, idx;
  while ((idx = lower.indexOf(q, i)) !== -1) {
    if (idx > i) parts.push(line.slice(i, idx));
    parts.push(<mark key={key++} style={{ background: 'var(--fc-accent)', color: 'var(--fc-term-bg)' }}>{line.slice(idx, idx + q.length)}</mark>);
    i = idx + q.length;
  }
  if (i < line.length) parts.push(line.slice(i));
  return parts.length ? parts : (line || ' ');
}

import { useRef, useCallback } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import type { BlockExecState } from '../stores/slices/executionSlice';

const stateColors: Record<BlockExecState, string> = {
  idle: '#555',
  running: '#4a9eff',
  success: '#2ecc71',
  error: '#e74c3c',
  skipped: '#888',
  disabled: '#555',
};

export default function TimelinePanel() {
  const timelineEntries = useFlowStore((s) => s.timelineEntries);
  const timelineIndex = useFlowStore((s) => s.timelineIndex);
  const timelineScrubbing = useFlowStore((s) => s.timelineScrubbing);
  const isRunning = useFlowStore((s) => s.isRunning);
  const scrubTo = useFlowStore((s) => s.scrubTo);
  const stopScrubbing = useFlowStore((s) => s.stopScrubbing);

  const tooltipRef = useRef<HTMLDivElement>(null);
  const tooltipDataRef = useRef<{ label: string; duration: string } | null>(null);

  const maxDuration = timelineEntries.reduce(
    (max, e) => Math.max(max, e.duration ?? 0),
    1,
  );

  const getBarWidth = useCallback(
    (duration: number | undefined) => {
      const d = duration ?? 0;
      const normalized = maxDuration > 0 ? d / maxDuration : 0;
      return Math.max(20, Math.min(80, 20 + normalized * 60));
    },
    [maxDuration],
  );

  const handleMouseEnter = useCallback(
    (e: React.MouseEvent, label: string, duration: number | undefined) => {
      const tooltip = tooltipRef.current;
      if (!tooltip) return;
      const durationStr = duration !== undefined ? `${duration}ms` : 'running...';
      tooltipDataRef.current = { label, duration: durationStr };
      tooltip.textContent = `${label} \u2014 ${durationStr}`;
      tooltip.style.display = 'block';
      const rect = (e.target as HTMLElement).getBoundingClientRect();
      tooltip.style.left = `${rect.left}px`;
      tooltip.style.top = `${rect.top - 28}px`;
    },
    [],
  );

  const handleMouseLeave = useCallback(() => {
    const tooltip = tooltipRef.current;
    if (tooltip) tooltip.style.display = 'none';
  }, []);

  const handleClick = useCallback(
    (index: number) => {
      if (timelineScrubbing && timelineIndex === index) {
        stopScrubbing();
      } else {
        scrubTo(index);
      }
    },
    [timelineScrubbing, timelineIndex, scrubTo, stopScrubbing],
  );

  return (
    <div
      style={{
        borderTop: '1px solid #2a2a4a',
        padding: 8,
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
      }}
    >
      {/* Header */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          fontSize: 11,
          color: '#888',
          fontWeight: 600,
          textTransform: 'uppercase',
          letterSpacing: '0.5px',
        }}
      >
        <span>Timeline</span>
        <span style={{ fontWeight: 400, fontSize: 10, color: '#666' }}>
          {timelineEntries.length > 0
            ? `${timelineEntries.length} step${timelineEntries.length !== 1 ? 's' : ''}`
            : ''}
        </span>
      </div>

      {/* Timeline bar area */}
      {timelineEntries.length === 0 ? (
        <div
          style={{
            fontSize: 11,
            color: '#555',
            textAlign: 'center',
            padding: '8px 0',
          }}
        >
          Run a script to see execution timeline
        </div>
      ) : (
        <div
          style={{
            display: 'flex',
            gap: 2,
            overflowX: 'auto',
            overflowY: 'hidden',
            paddingBottom: 2,
          }}
        >
          {timelineEntries.map((entry) => {
            const isActive = timelineIndex === entry.index;
            const color = stateColors[entry.state] || '#555';
            const width = getBarWidth(entry.duration);

            return (
              <div
                key={entry.index}
                onClick={() => handleClick(entry.index)}
                onMouseEnter={(e) =>
                  handleMouseEnter(e, entry.nodeLabel || entry.blockType, entry.duration)
                }
                onMouseLeave={handleMouseLeave}
                style={{
                  width,
                  height: 18,
                  background: color,
                  borderRadius: 3,
                  cursor: 'pointer',
                  flexShrink: 0,
                  opacity: isActive ? 1 : 0.6,
                  border: isActive ? '1px solid #fff' : '1px solid transparent',
                  transition: 'opacity 0.15s, border-color 0.15s',
                }}
                title={`${entry.nodeLabel || entry.blockType}${entry.duration !== undefined ? ` \u2014 ${entry.duration}ms` : ''}`}
              />
            );
          })}

          {isRunning && (
            <div
              style={{
                width: 20,
                height: 18,
                background: '#4a9eff',
                borderRadius: 3,
                flexShrink: 0,
                opacity: 0.4,
                animation: 'pulse 1s infinite',
              }}
            />
          )}
        </div>
      )}

      {/* Tooltip (positioned fixed for overflow scenarios) */}
      <div
        ref={tooltipRef}
        style={{
          display: 'none',
          position: 'fixed',
          zIndex: 100,
          background: '#1a1a2e',
          border: '1px solid #2a2a4a',
          borderRadius: 4,
          padding: '3px 8px',
          fontSize: 10,
          color: '#ccc',
          whiteSpace: 'nowrap',
          pointerEvents: 'none',
        }}
      />

      <style>{`
        @keyframes pulse {
          0%, 100% { opacity: 0.4; }
          50% { opacity: 0.8; }
        }
      `}</style>
    </div>
  );
}

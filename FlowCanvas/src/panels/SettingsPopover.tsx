import { useEffect, useRef, useState, type CSSProperties } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { WIDTH_PRESETS, TEXT_SCALES, DENSITIES } from '../stores/slices/settingsSlice';
import { mix } from '../utils/tokens';

// Label on its own line above a FULL-WIDTH segmented row whose chips share the width equally
// (flex: 1). Stacking + equal-flex is what lets all 5 width presets fit the popover without
// clipping — an inline chip group can't shrink and overflows once the labels are real words.
function Segmented<T extends string | number>(props: {
  label: string;
  value: T;
  options: readonly { label: string; v: T }[];
  onChange: (v: T) => void;
}) {
  return (
    <div style={{ padding: '5px 0' }} data-testid={`setting-${props.label.toLowerCase().replace(/\s+/g, '-')}`}>
      <div style={{ ...labStyle, marginBottom: 4 }}>{props.label}</div>
      <div style={{ display: 'flex', width: '100%', border: '1px solid var(--fc-border)', borderRadius: 6, overflow: 'hidden' }}>
        {props.options.map((o, i) => {
          const on = o.v === props.value;
          return (
            <button key={o.label} onClick={() => props.onChange(o.v)} style={{
              flex: '1 1 0', minWidth: 0, fontSize: 10, padding: '4px 2px', cursor: 'pointer', fontFamily: 'inherit',
              textAlign: 'center', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
              border: 'none', borderLeft: i ? '1px solid var(--fc-border)' : 'none',
              background: on ? 'var(--fc-accent-surface)' : 'var(--fc-surface-1)',
              color: on ? 'var(--fc-text)' : 'var(--fc-text-muted)',
            }}>{o.label}</button>
          );
        })}
      </div>
    </div>
  );
}

function Toggle(props: { label: string; on: boolean; onClick: () => void }) {
  return (
    <div style={rowStyle}>
      <span style={labStyle}>{props.label}</span>
      <button onClick={props.onClick} role="switch" aria-checked={props.on} style={{
        width: 30, height: 16, borderRadius: 9, border: 'none', cursor: 'pointer', position: 'relative',
        background: props.on ? 'var(--fc-accent-surface)' : 'var(--fc-surface-2)',
      }}>
        <span style={{
          position: 'absolute', top: 2, left: props.on ? 16 : 2, width: 12, height: 12, borderRadius: '50%',
          background: props.on ? 'var(--fc-accent)' : 'var(--fc-text-muted)', transition: 'left 0.12s',
        }} />
      </button>
    </div>
  );
}

const rowStyle: CSSProperties = { display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10, padding: '5px 0' };
const labStyle: CSSProperties = { fontSize: 11, color: 'var(--fc-text-secondary)' };
const groupStyle: CSSProperties = { fontSize: 9, fontWeight: 700, letterSpacing: '0.6px', color: 'var(--fc-text-muted)', textTransform: 'uppercase', margin: '8px 0 2px' };

export default function SettingsPopover() {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const blockWidth = useFlowStore((s) => s.blockWidth);
  const textScale = useFlowStore((s) => s.textScale);
  const density = useFlowStore((s) => s.density);
  const defaultBlockExpanded = useFlowStore((s) => s.defaultBlockExpanded);
  const setBlockWidth = useFlowStore((s) => s.setBlockWidth);
  const setTextScale = useFlowStore((s) => s.setTextScale);
  const setDensity = useFlowStore((s) => s.setDensity);
  const setDefaultBlockExpanded = useFlowStore((s) => s.setDefaultBlockExpanded);
  const resetCanvasSettings = useFlowStore((s) => s.resetCanvasSettings);

  const snapToGrid = useFlowStore((s) => s.snapToGrid);
  const toggleSnapToGrid = useFlowStore((s) => s.toggleSnapToGrid);
  const branchBandsEnabled = useFlowStore((s) => s.branchBandsEnabled);
  const toggleBranchBands = useFlowStore((s) => s.toggleBranchBands);
  const heatmapEnabled = useFlowStore((s) => s.heatmapEnabled);
  const toggleHeatmap = useFlowStore((s) => s.toggleHeatmap);
  const reducedMotion = useFlowStore((s) => s.reducedMotion);
  const toggleReducedMotion = useFlowStore((s) => s.toggleReducedMotion);

  useEffect(() => {
    if (!open) return;
    const onDoc = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false); };
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false); };
    document.addEventListener('mousedown', onDoc);
    document.addEventListener('keydown', onKey);
    return () => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey); };
  }, [open]);

  // Widen literal-typed preset arrays to the plain primitive type that the generic Segmented expects.
  const widthOptions: readonly { label: string; v: number }[] = WIDTH_PRESETS.map((p) => ({ label: p.label, v: p.px as number }));
  const textOptions: readonly { label: string; v: number }[] = TEXT_SCALES.map((p) => ({ label: p.label, v: p.v as number }));
  const densityOptions: readonly { label: string; v: number }[] = DENSITIES.map((p) => ({ label: p.label, v: p.v as number }));
  const newBlocksOptions: readonly { label: string; v: number }[] = [{ label: 'Collapsed', v: 0 }, { label: 'Expanded', v: 1 }];

  return (
    <div ref={ref} style={{ position: 'relative' }}>
      <button
        onClick={() => setOpen((o) => !o)}
        title="Display settings"
        aria-haspopup="dialog"
        aria-expanded={open}
        style={{
          padding: '4px 8px', borderRadius: 4, fontFamily: 'inherit', fontSize: 12, cursor: 'pointer',
          background: 'var(--fc-surface-2)',
          border: `1px solid ${open ? mix('var(--fc-accent)', 50) : 'var(--fc-border)'}`,
          color: open ? 'var(--fc-accent)' : 'var(--fc-text-secondary)',
        }}
      >
        ⚙
      </button>

      {open && (
        <div role="dialog" aria-label="Display settings" style={{
          position: 'absolute', top: 'calc(100% + 6px)', right: 0, zIndex: 50, width: 280,
          background: 'var(--fc-surface-0)', border: '1px solid var(--fc-border-subtle)',
          borderRadius: 9, boxShadow: '0 12px 36px var(--fc-overlay-scrim)', padding: '8px 12px 10px',
        }}>
          <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '0.5px', color: 'var(--fc-text)', padding: '2px 0 2px' }}>
            DISPLAY SETTINGS
          </div>

          <div style={groupStyle}>Sizing</div>
          <Segmented label="Block width" value={blockWidth} options={widthOptions} onChange={setBlockWidth} />
          <Segmented label="Text size" value={textScale} options={textOptions} onChange={setTextScale} />
          <Segmented label="Canvas density" value={density} options={densityOptions} onChange={setDensity} />
          <Segmented
            label="New blocks"
            value={defaultBlockExpanded ? 1 : 0}
            options={newBlocksOptions}
            onChange={(v) => setDefaultBlockExpanded(v === 1)}
          />

          <div style={{ height: 1, background: 'var(--fc-border)', margin: '8px 0 2px' }} />
          <div style={groupStyle}>View</div>
          <Toggle label="Snap to grid" on={snapToGrid} onClick={toggleSnapToGrid} />
          <Toggle label="Branch bands" on={branchBandsEnabled} onClick={toggleBranchBands} />
          <Toggle label="Heatmap" on={heatmapEnabled} onClick={toggleHeatmap} />
          <Toggle label="Reduced motion" on={reducedMotion} onClick={toggleReducedMotion} />

          <div style={{ height: 1, background: 'var(--fc-border)', margin: '8px 0 4px' }} />
          <button onClick={resetCanvasSettings} style={{
            background: 'none', border: 'none', color: 'var(--fc-accent)', fontSize: 10, cursor: 'pointer',
            fontFamily: 'inherit', padding: '2px 0',
          }}>↺ Reset to defaults</button>
        </div>
      )}
    </div>
  );
}

import { type DragEvent } from 'react';
import {
  getBlocksByCategory,
  categoryLabels,
  categoryColors,
  type BlockDef,
  type BlockCategory,
} from '../blockDefs/registry';
import { mix } from '../utils/tokens';
import { BlockIcon } from '../nodes/BlockIcon';

const categoryOrder: BlockCategory[] = [
  'ssh', 'control-flow', 'data', 'network', 'io', 'grid', 'timing',
];

function onDragStart(event: DragEvent, blockType: string) {
  event.dataTransfer.setData('application/flowcanvas-block', blockType);
  event.dataTransfer.effectAllowed = 'move';
}

function PaletteItem({ def }: { def: BlockDef }) {
  const colors = categoryColors[def.category];
  return (
    <div
      draggable
      onDragStart={(e) => onDragStart(e, def.type)}
      title={def.description}
      style={{
        padding: '4px 8px',
        background: colors.bg,
        border: `1px solid ${mix(colors.border, 33)}`,
        borderRadius: 4,
        fontSize: 12,
        color: colors.text,
        cursor: 'grab',
        display: 'flex',
        alignItems: 'center',
        gap: 6,
        userSelect: 'none',
        transition: 'border-color 0.15s',
      }}
      onMouseEnter={(e) => (e.currentTarget.style.borderColor = colors.border)}
      onMouseLeave={(e) => (e.currentTarget.style.borderColor = mix(colors.border, 33))}
    >
      <span style={{ color: colors.icon, display: 'flex', flexShrink: 0 }}>
        <BlockIcon name={def.icon} size={14} />
      </span>
      <span>{def.label}</span>
    </div>
  );
}

const byCategory = getBlocksByCategory(); // computed once at module level

export default function Palette() {

  return (
    <div style={{
      width: 180,
      background: 'var(--fc-surface-0)',
      borderRight: '1px solid var(--fc-border)',
      overflowY: 'auto',
      padding: '8px',
      display: 'flex',
      flexDirection: 'column',
      gap: 12,
      flexShrink: 0,
    }}>
      <div style={{
        fontSize: 11,
        fontWeight: 600,
        color: 'var(--fc-text-faint)',
        textTransform: 'uppercase',
        letterSpacing: '1px',
        padding: '4px 0',
      }}>
        Blocks
      </div>

      {categoryOrder.map((cat) => {
        const defs = byCategory.get(cat);
        if (!defs) return null;
        return (
          <div key={cat}>
            <div style={{
              fontSize: 10,
              color: categoryColors[cat].border,
              textTransform: 'uppercase',
              letterSpacing: '0.8px',
              marginBottom: 4,
              fontWeight: 600,
            }}>
              {categoryLabels[cat]}
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
              {defs.map((def) => (
                <PaletteItem key={def.type} def={def} />
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );
}

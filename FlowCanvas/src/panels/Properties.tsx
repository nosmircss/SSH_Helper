import { useCallback } from 'react';
import { useReactFlow, type Node } from '@xyflow/react';
import { blockDefMap, categoryColors, type BlockCategory, type PropertyDef } from '../blockDefs/registry';
import type { BlockNodeData } from '../nodes/BaseBlock';

interface PropertiesProps {
  selectedNodeId: string | null;
}

function PropertyField({
  def,
  value,
  onChange,
  colors,
}: {
  def: PropertyDef;
  value: unknown;
  onChange: (val: unknown) => void;
  colors: { text: string; border: string; bg: string };
}) {
  const inputStyle: React.CSSProperties = {
    width: '100%',
    padding: '4px 6px',
    background: '#0d1117',
    border: `1px solid ${colors.border}44`,
    borderRadius: 4,
    color: colors.text,
    fontSize: 12,
    fontFamily: def.type === 'code' ? 'monospace' : 'inherit',
    outline: 'none',
  };

  switch (def.type) {
    case 'boolean':
      return (
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, color: '#aaa', cursor: 'pointer' }}>
          <input
            type="checkbox"
            checked={!!value}
            onChange={(e) => onChange(e.target.checked)}
            style={{ accentColor: colors.border }}
          />
          {def.label}
        </label>
      );

    case 'select':
      return (
        <select
          value={String(value ?? def.defaultValue ?? '')}
          onChange={(e) => onChange(e.target.value)}
          style={{ ...inputStyle, cursor: 'pointer' }}
        >
          <option value="">—</option>
          {def.options?.map((opt) => (
            <option key={opt} value={opt}>{opt}</option>
          ))}
        </select>
      );

    case 'number':
      return (
        <input
          type="number"
          value={value !== undefined && value !== null ? String(value) : ''}
          placeholder={def.placeholder}
          onChange={(e) => onChange(e.target.value ? Number(e.target.value) : undefined)}
          style={inputStyle}
        />
      );

    case 'textarea':
      return (
        <textarea
          value={String(value ?? '')}
          placeholder={def.placeholder}
          onChange={(e) => onChange(e.target.value)}
          rows={3}
          style={{ ...inputStyle, resize: 'vertical' }}
        />
      );

    case 'code':
    case 'text':
    default:
      return (
        <input
          type="text"
          value={String(value ?? '')}
          placeholder={def.placeholder}
          onChange={(e) => onChange(e.target.value)}
          style={inputStyle}
        />
      );
  }
}

export default function Properties({ selectedNodeId }: PropertiesProps) {
  const { getNode, setNodes } = useReactFlow();

  const node = selectedNodeId ? getNode(selectedNodeId) : null;
  const blockData = node?.data as BlockNodeData | undefined;
  const def = blockData?.blockType ? blockDefMap.get(blockData.blockType) : null;

  const updateProp = useCallback(
    (key: string, value: unknown) => {
      if (!selectedNodeId) return;
      setNodes((nds) =>
        nds.map((n) => {
          if (n.id !== selectedNodeId) return n;
          const data = n.data as BlockNodeData;
          return {
            ...n,
            data: {
              ...data,
              props: { ...data.props, [key]: value },
            },
          };
        }),
      );
    },
    [selectedNodeId, setNodes],
  );

  const updateLabel = useCallback(
    (label: string) => {
      if (!selectedNodeId) return;
      setNodes((nds) =>
        nds.map((n) => {
          if (n.id !== selectedNodeId) return n;
          return { ...n, data: { ...n.data, label } };
        }),
      );
    },
    [selectedNodeId, setNodes],
  );

  if (!node || !def || !blockData) {
    return (
      <div style={{
        flex: 1,
        padding: 16,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}>
        <span style={{ color: '#555', fontSize: 12, textAlign: 'center' }}>
          Select a block to edit its properties
        </span>
      </div>
    );
  }

  const colors = categoryColors[def.category as BlockCategory];
  const isChild = !!(blockData.props?.['_isChildOf']);
  const branchLabel = blockData.props?.['_branchLabel'] as string | undefined;
  const branchColor = blockData.props?.['_branchColor'] as string | undefined;

  // Read-only view for child nodes (nested inside container blocks)
  if (isChild) {
    return (
      <div style={{
        flex: 1,
        overflowY: 'auto',
        padding: 12,
        display: 'flex',
        flexDirection: 'column',
        gap: 12,
      }}>
        {/* Header with nested indicator */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, paddingBottom: 8, borderBottom: '1px solid #2a2a4a' }}>
          <span style={{
            background: colors.badge,
            color: colors.badgeText,
            fontSize: 10,
            fontWeight: 700,
            padding: '2px 6px',
            borderRadius: 3,
            textTransform: 'uppercase',
          }}>
            {def.type}
          </span>
          <span style={{ color: '#ccc', fontSize: 12, fontWeight: 600 }}>
            {def.label}
          </span>
        </div>

        {/* Branch indicator */}
        {branchLabel && (
          <div style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            padding: '4px 8px',
            background: `${branchColor || '#555'}15`,
            borderRadius: 4,
            borderLeft: `2px solid ${branchColor || '#555'}`,
          }}>
            <span style={{ fontSize: 10, color: branchColor || '#888', fontWeight: 600, textTransform: 'uppercase' }}>
              {branchLabel}
            </span>
            <span style={{ fontSize: 10, color: '#666' }}>branch (read-only)</span>
          </div>
        )}

        {/* Property values (read-only) */}
        {def.properties.map((propDef) => {
          const val = blockData.props?.[propDef.key];
          if (val === undefined || val === null || val === '') return null;
          return (
            <div key={propDef.key}>
              <label style={{ fontSize: 11, color: '#666', display: 'block', marginBottom: 3 }}>
                {propDef.label}
              </label>
              <div style={{
                padding: '4px 6px',
                background: '#0d111799',
                borderRadius: 4,
                color: '#aaa',
                fontSize: 12,
                fontFamily: propDef.type === 'code' ? 'monospace' : 'inherit',
                whiteSpace: 'pre-wrap',
                wordBreak: 'break-all',
              }}>
                {String(val)}
              </div>
            </div>
          );
        })}

        {/* Description */}
        <div style={{
          marginTop: 'auto',
          paddingTop: 12,
          borderTop: '1px solid #2a2a4a',
          fontSize: 11,
          color: '#555',
          lineHeight: 1.5,
        }}>
          {def.description}
        </div>
      </div>
    );
  }

  return (
    <div style={{
      flex: 1,
      overflowY: 'auto',
      padding: 12,
      display: 'flex',
      flexDirection: 'column',
      gap: 12,
    }}>
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, paddingBottom: 8, borderBottom: '1px solid #2a2a4a' }}>
        <span style={{
          background: colors.badge,
          color: colors.badgeText,
          fontSize: 10,
          fontWeight: 700,
          padding: '2px 6px',
          borderRadius: 3,
          textTransform: 'uppercase',
        }}>
          {def.type}
        </span>
        <span style={{ color: '#ccc', fontSize: 12, fontWeight: 600 }}>
          {def.label}
        </span>
      </div>

      {/* Label field */}
      <div>
        <label style={{ fontSize: 11, color: '#666', display: 'block', marginBottom: 3 }}>Display Name</label>
        <input
          type="text"
          value={String(blockData.label ?? '')}
          placeholder={def.label}
          onChange={(e) => updateLabel(e.target.value)}
          style={{
            width: '100%',
            padding: '4px 6px',
            background: '#0d1117',
            border: `1px solid ${colors.border}44`,
            borderRadius: 4,
            color: '#ccc',
            fontSize: 12,
            outline: 'none',
          }}
        />
      </div>

      {/* Property fields */}
      {def.properties.map((propDef) => (
        <div key={propDef.key}>
          {propDef.type !== 'boolean' && (
            <label style={{ fontSize: 11, color: '#666', display: 'block', marginBottom: 3 }}>
              {propDef.label}
              {propDef.required && <span style={{ color: '#e74c3c', marginLeft: 2 }}>*</span>}
            </label>
          )}
          <PropertyField
            def={propDef}
            value={blockData.props?.[propDef.key]}
            onChange={(val) => updateProp(propDef.key, val)}
            colors={colors}
          />
        </div>
      ))}

      {/* Description */}
      <div style={{
        marginTop: 'auto',
        paddingTop: 12,
        borderTop: '1px solid #2a2a4a',
        fontSize: 11,
        color: '#555',
        lineHeight: 1.5,
      }}>
        {def.description}
      </div>
    </div>
  );
}

import { useFlowStore } from '../stores/useFlowStore';

/** Key columns to suppress from the variables display (already shown explicitly). */
const SUPPRESSED_KEYS = new Set(['Host_IP', 'port', 'username', 'password']);

/** Max number of extra variable chips to show before truncating. */
const MAX_VARS = 4;

export default function HostBar() {
  const targetHost = useFlowStore((s) => s.targetHost);
  const theme = useFlowStore((s) => s.theme);

  const isDark = theme === 'dark';
  const barBg = isDark ? '#0f1a2e' : '#e8ecf4';
  const borderColor = isDark ? '#1a3a5c' : '#c0c8d8';
  const accentColor = isDark ? '#4ecca3' : '#1a8a5a';
  const labelColor = isDark ? '#667' : '#889';
  const valueColor = isDark ? '#ccd' : '#334';
  const mutedColor = isDark ? '#556' : '#999';

  if (!targetHost) {
    return (
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          padding: '4px 12px',
          background: barBg,
          borderBottom: `1px solid ${borderColor}`,
          fontSize: 11,
          color: mutedColor,
          fontStyle: 'italic',
          minHeight: 28,
        }}
      >
        No target host — select a host in the main grid
      </div>
    );
  }

  // Collect non-empty variables that aren't already shown explicitly.
  const extraVars = Object.entries(targetHost.variables)
    .filter(([k, v]) => !SUPPRESSED_KEYS.has(k) && v !== '')
    .slice(0, MAX_VARS);

  const showPort = targetHost.port !== 22;

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        padding: '4px 12px',
        background: barBg,
        borderBottom: `1px solid ${borderColor}`,
        gap: 14,
        fontSize: 11,
        minHeight: 28,
        overflow: 'hidden',
      }}
    >
      <span style={{ color: accentColor, fontWeight: 700, whiteSpace: 'nowrap' }}>
        &#x1F3AF; TARGET
      </span>

      <Chip label="Host" value={targetHost.ip} labelColor={labelColor} valueColor={valueColor} />
      {showPort && (
        <Chip label="Port" value={String(targetHost.port)} labelColor={labelColor} valueColor={valueColor} />
      )}
      {targetHost.username && (
        <Chip label="User" value={targetHost.username} labelColor={labelColor} valueColor={valueColor} />
      )}

      {extraVars.map(([k, v]) => (
        <Chip key={k} label={k} value={v} labelColor={labelColor} valueColor={valueColor} />
      ))}

      <span style={{ marginLeft: 'auto', color: mutedColor, fontStyle: 'italic', whiteSpace: 'nowrap', fontSize: 10 }}>
        Change in main grid
      </span>
    </div>
  );
}

function Chip({
  label,
  value,
  labelColor,
  valueColor,
}: {
  label: string;
  value: string;
  labelColor: string;
  valueColor: string;
}) {
  return (
    <span style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', maxWidth: 180 }}>
      <span style={{ color: labelColor }}>{label}:</span>{' '}
      <span style={{ color: valueColor }}>{value}</span>
    </span>
  );
}

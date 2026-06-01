import { useFlowStore } from '../stores/useFlowStore';

/** Key columns to suppress from the variables display (already shown explicitly). */
const SUPPRESSED_KEYS = new Set(['Host_IP', 'port', 'username', 'password']);

/** Max number of extra variable chips to show before truncating. */
const MAX_VARS = 4;

export default function HostBar() {
  const targetHost = useFlowStore((s) => s.targetHost);

  // HostBar ships dark-only; values come from the token layer (styles/tokens.css).
  const barBg = 'var(--fc-host-bg)';
  const borderColor = 'var(--fc-border)';
  const accentColor = 'var(--fc-host-accent)';
  const labelColor = 'var(--fc-text-muted)';
  const valueColor = 'var(--fc-text)';
  const mutedColor = 'var(--fc-text-disabled)';

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

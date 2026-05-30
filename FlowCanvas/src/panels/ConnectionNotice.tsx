import { useEffect } from 'react';
import { useFlowStore } from '../stores/useFlowStore';

export default function ConnectionNotice() {
  const notice = useFlowStore((s) => s.connectionNotice);
  const clear = useFlowStore((s) => s.clearConnectionNotice);

  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(clear, 2500);
    return () => clearTimeout(t);
  }, [notice, clear]);

  if (!notice) return null;
  return (
    <div style={{
      position: 'absolute', top: 16, left: '50%', transform: 'translateX(-50%)', zIndex: 30,
      padding: '8px 14px', maxWidth: 420,
      background: 'var(--fc-notice-bg)', color: 'var(--fc-notice-fg)',
      border: '1px solid var(--fc-notice-border)', borderRadius: 'var(--fc-radius-md)',
      boxShadow: 'var(--fc-shadow-sm)', fontSize: 'var(--fc-fs-body)',
    }}>
      {notice.message}
    </div>
  );
}

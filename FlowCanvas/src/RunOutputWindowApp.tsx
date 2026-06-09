/** Standalone entry for the detached Run Output window (?panel=runoutput). Renders only the
 *  console and wires the minimal window bridge. */
import { useEffect } from 'react';
import { useFlowStore } from './stores/useFlowStore';
import RunOutputView from './panels/RunOutputView';
import { initRunOutputWindowBridge } from './stores/runOutputWindowBridge';

export default function RunOutputWindowApp() {
  const setPoppedOut = useFlowStore((s) => s.setRunOutputPoppedOut);
  useEffect(() => {
    // This window IS the popped-out console, so hide the console's own "Pop out" button.
    setPoppedOut(true);
    const cleanup = initRunOutputWindowBridge();
    return cleanup;
  }, [setPoppedOut]);

  return (
    <div style={{ height: '100vh', display: 'flex', flexDirection: 'column', background: 'var(--fc-term-bg)' }}>
      <RunOutputView />
    </div>
  );
}

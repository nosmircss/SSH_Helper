import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import { useFlowStore } from '../../useFlowStore';
import { sendLayoutAutosave } from '../../../utils/layoutAutosave';

describe('expandedNodes', () => {
  beforeEach(() => { useFlowStore.setState({ expandedNodes: new Set() }); vi.clearAllMocks(); });

  it('toggleExpanded adds/removes and reports via isExpanded', () => {
    const s = useFlowStore.getState();
    s.toggleExpanded('n1');
    expect(useFlowStore.getState().isExpanded('n1')).toBe(true);
    s.toggleExpanded('n1');
    expect(useFlowStore.getState().isExpanded('n1')).toBe(false);
  });

  it('toggleExpanded persists via sendLayoutAutosave', () => {
    useFlowStore.getState().toggleExpanded('n1');
    expect(sendLayoutAutosave).toHaveBeenCalled();
  });

  it('restoreExpandedNodes replaces the set', () => {
    useFlowStore.getState().restoreExpandedNodes(['a', 'b']);
    expect(useFlowStore.getState().isExpanded('a')).toBe(true);
    expect(useFlowStore.getState().expandedNodes.size).toBe(2);
  });
});

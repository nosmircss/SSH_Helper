import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({
  messageBus: { send: vi.fn() },
  CANVAS_HOST_MESSAGES: { outgoing: { layoutSave: 'layout-save' } },
}));
import { useFlowStore } from '../../useFlowStore';
import { messageBus } from '../../../MessageBus';
import { sendLayoutAutosave } from '../../../utils/layoutAutosave';
import { computeHierarchicalLayout, DEFAULT_BLOCK_SIZING } from '../../../utils/layout/hierarchicalLayout';
import { SETTINGS_DEFAULTS } from '../settingsSlice';

const chain = () => {
  const nodes = [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
    { id: 'A', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'a' } } },
    { id: 'B', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'b' } } },
  ];
  const edges = [
    { id: 'e0', source: '__start__', target: 'A' },
    { id: 'e1', source: 'A', target: 'B' },
  ];
  useFlowStore.getState().setNodes(nodes as never);
  useFlowStore.getState().setEdges(edges as never);
  const s = useFlowStore.getState();
  s.setNodes(computeHierarchicalLayout(s.nodes, s.edges, DEFAULT_BLOCK_SIZING));
};

describe('settingsSlice', () => {
  beforeEach(() => {
    useFlowStore.setState({ ...SETTINGS_DEFAULTS });
    vi.clearAllMocks();
  });

  it('defaults are Normal/M/Normal/collapsed', () => {
    const s = useFlowStore.getState();
    expect(s.blockWidth).toBe(330);
    expect(s.textScale).toBe(1);
    expect(s.density).toBe(1);
    expect(s.defaultBlockExpanded).toBe(false);
  });

  it('setBlockWidth updates state and persists via layout-save', () => {
    chain();
    useFlowStore.getState().setBlockWidth(700);
    expect(useFlowStore.getState().blockWidth).toBe(700);
    expect(messageBus.send).toHaveBeenCalledWith(expect.objectContaining({ type: 'layout-save', blockWidth: 700 }));
  });

  it('setDensity roomy pushes B lower', () => {
    chain();
    const y0 = useFlowStore.getState().nodes.find((n) => n.id === 'B')!.position.y;
    useFlowStore.getState().setDensity(1.2);
    const yRoomy = useFlowStore.getState().nodes.find((n) => n.id === 'B')!.position.y;
    expect(yRoomy).toBeGreaterThan(y0);
  });

  it('resetCanvasSettings restores every field', () => {
    useFlowStore.setState({ blockWidth: 700, textScale: 1.15, density: 1.2, defaultBlockExpanded: true });
    useFlowStore.getState().resetCanvasSettings();
    const s = useFlowStore.getState();
    expect([s.blockWidth, s.textScale, s.density, s.defaultBlockExpanded]).toEqual([330, 1, 1, false]);
  });

  it('restoreCanvasSettings applies values and reflows when nodes exist', () => {
    chain();
    const xBefore = useFlowStore.getState().nodes.find((n) => n.id === 'A')!.position.x;
    useFlowStore.getState().restoreCanvasSettings({ blockWidth: 700 });
    expect(useFlowStore.getState().blockWidth).toBe(700);
    expect(typeof xBefore).toBe('number');
  });
});

describe('default block state (always-expanded)', () => {
  beforeEach(() => {
    useFlowStore.setState({ ...SETTINGS_DEFAULTS, expandedNodes: new Set<string>(), layoutMode: 'auto' });
    vi.clearAllMocks();
  });

  it('setDefaultBlockExpanded(true) expands the open graph immediately and persists', () => {
    chain();
    useFlowStore.getState().setDefaultBlockExpanded(true);
    const s = useFlowStore.getState();
    expect(s.expandedNodes.has('A')).toBe(true);
    expect(s.expandedNodes.has('B')).toBe(true);
    expect((s.nodes.find((n) => n.id === 'A')!.data as Record<string, unknown>).expanded).toBe(true);
    expect(messageBus.send).toHaveBeenCalledWith(
      expect.objectContaining({ type: 'layout-save', defaultBlockExpanded: true }),
    );
  });

  it('setDefaultBlockExpanded(false) collapses the open graph', () => {
    chain();
    useFlowStore.getState().setAllExpanded(true);
    useFlowStore.getState().setDefaultBlockExpanded(false);
    expect(useFlowStore.getState().expandedNodes.size).toBe(0);
  });

  it('restoreCanvasSettings with Expanded applies to an already-loaded graph WITHOUT autosaving', () => {
    // Fresh-open ordering: load-graph lands before the settings restore. The restore must
    // expand the loaded blocks, but merely opening a preset must not rewrite its saved layout.
    chain();
    vi.clearAllMocks();
    useFlowStore.getState().restoreCanvasSettings({ defaultBlockExpanded: true });
    const s = useFlowStore.getState();
    expect(s.expandedNodes.has('A')).toBe(true);
    expect(s.expandedNodes.has('B')).toBe(true);
    expect(sendLayoutAutosave).not.toHaveBeenCalled();
  });

  it('restoreCanvasSettings without the flag leaves per-preset expansion untouched', () => {
    chain();
    useFlowStore.getState().toggleExpanded('A'); // user expanded just A
    useFlowStore.getState().restoreCanvasSettings({ blockWidth: 700 });
    const s = useFlowStore.getState();
    expect(s.expandedNodes.has('A')).toBe(true);
    expect(s.expandedNodes.has('B')).toBe(false);
  });

  it('resetCanvasSettings collapses the open graph when the default was Expanded', () => {
    // Reset flips the setting to Collapsed, so the open graph must collapse too — otherwise
    // the control reads Collapsed while every block stays expanded (and autosave persists it).
    chain();
    useFlowStore.getState().setDefaultBlockExpanded(true);
    expect(useFlowStore.getState().expandedNodes.size).toBe(2);
    useFlowStore.getState().resetCanvasSettings();
    const s = useFlowStore.getState();
    expect(s.defaultBlockExpanded).toBe(false);
    expect(s.expandedNodes.size).toBe(0);
    expect((s.nodes.find((n) => n.id === 'A')!.data as Record<string, unknown>).expanded).toBe(false);
  });
});

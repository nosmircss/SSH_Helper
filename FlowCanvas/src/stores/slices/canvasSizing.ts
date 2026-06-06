import type { BlockSizing } from '../../utils/layout/hierarchicalLayout';
import type { SettingsSlice } from './settingsSlice';

/**
 * Pure selector mapping the live Display Settings to the BlockSizing the layout needs.
 *
 * Lives in its own runtime-leaf module (type-only imports) so layout/reflow helpers can use it
 * without importing settingsSlice — which pulls layoutAutosave -> useFlowStore and would form an
 * init-order cycle once a first-loaded slice (graphSlice) depends on it via reflow.
 */
export const selectCanvasSizing = (
  s: Pick<SettingsSlice, 'blockWidth' | 'density' | 'textScale'> & { compactCommentsEnabled?: boolean },
): BlockSizing => ({
  blockWidth: s.blockWidth,
  density: s.density,
  textScale: s.textScale,
  compactComments: s.compactCommentsEnabled,
});

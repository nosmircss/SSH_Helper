import { expect, test, type Page } from '@playwright/test';
import {
  clearOutgoingMessages,
  getGraphSnapshot,
  installHostMessageCapture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

// Imported if/else with deliberately scattered/overlapping positions.
const messyIfElse = {
  nodes: [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', label: 'Start', props: {} } },
    { id: 'if-1', type: 'block', position: { x: 500, y: 500 }, data: { blockType: 'if', label: 'If', props: { condition: '${x}', _stepPath: 'steps/0' } } },
    { id: 'then-1', type: 'block', position: { x: 505, y: 505 }, data: { blockType: 'print', label: 'Then', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/0', _branchLabel: 'then', message: 't' } } },
    { id: 'else-1', type: 'block', position: { x: 510, y: 510 }, data: { blockType: 'print', label: 'Else', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/else/0', _branchLabel: 'else', message: 'e' } } },
  ],
  edges: [
    { id: 'edge-start-if', source: '__start__', target: 'if-1', style: { stroke: '#666' } },
    { id: 'edge-if-then', source: 'if-1', target: 'then-1', label: 'then', style: { stroke: 'var(--fc-branch-then)' } },
    { id: 'edge-if-else', source: 'if-1', target: 'else-1', sourceHandle: 'false', label: 'else', style: { stroke: 'var(--fc-branch-else)' } },
  ],
};

async function posById(page: Page, id: string): Promise<{ x: number; y: number }> {
  const snap = await getGraphSnapshot(page);
  const n = (snap.nodes as Array<{ id: string; position: { x: number; y: number } }>).find((m) => m.id === id)!;
  return n.position;
}

test.describe('Flow Canvas Auto-Organize (hierarchical)', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('fresh import lays branches into clean, non-overlapping columns', async ({ page }) => {
    // no layoutAction:'keep' → engine runs on import (auto-flow).
    await postHostMessage(page, { type: 'load-graph', ...messyIfElse });
    await expect(page.locator('.react-flow__node[data-id="if-1"]')).toBeVisible();

    const then = await posById(page, 'then-1');
    const els = await posById(page, 'else-1');
    const ifp = await posById(page, 'if-1');

    expect(then.y).toBe(els.y);                       // sibling columns start level
    expect(then.x).toBeLessThan(els.x);               // then left of else
    expect(Math.abs(then.x - els.x)).toBeGreaterThanOrEqual(300); // no overlap
    expect(then.x).toBeGreaterThan(ifp.x);            // primary branch indented right (spine gutter)
    expect(els.x).toBeGreaterThan(then.x);            // additional branches spread further right
    expect(then.y).toBeGreaterThan(ifp.y);            // children below the container
  });

  test('Auto-organize button overrides a saved arrangement', async ({ page }) => {
    // layoutMode:'manual',layoutAction:'keep' → import keeps the messy positions...
    await postHostMessage(page, { type: 'load-graph', ...messyIfElse, layoutMode: 'manual', layoutAction: 'keep' });
    await expect(page.locator('.react-flow__node[data-id="if-1"]')).toBeVisible();
    expect((await posById(page, 'if-1')).x).toBe(500); // kept as-is

    // ...until the user presses the button, which re-lays everything.
    await page.getByRole('button', { name: /Auto-organize|Layout/ }).click();

    expect((await posById(page, 'if-1')).x).toBe(250); // NODE_START_X
    const then = await posById(page, 'then-1');
    const els = await posById(page, 'else-1');
    expect(then.x).toBeLessThan(els.x);
  });
});

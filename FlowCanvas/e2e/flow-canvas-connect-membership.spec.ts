import { expect, test, type Page } from '@playwright/test';
import {
  clearOutgoingMessages,
  connectViaActions,
  getGraphSnapshot,
  installHostMessageCapture,
  setGraphViaActions,
  waitForOutgoingMessage,
} from './support/harness';

// Imported outer IF (top-level, steps/0) with THEN = [a, innerIf]; innerIf is a nested container
// that is LAST in the branch, so its `continue` handle is free. A top-level `tail` follows the IF,
// and a fresh metadata-less `dropped` block sits off in the corner where the user dropped it.
function nestedFixture() {
  return {
    nodes: [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', label: 'Start', props: {} } },
      { id: 'outer-if', type: 'block', position: { x: 250, y: 140 }, data: { blockType: 'if', label: 'If', props: { condition: 'x > 0', _stepPath: 'steps/0' } } },
      { id: 'then-a', type: 'block', position: { x: 250, y: 260 }, data: { blockType: 'send', label: 'Send', props: { command: 'a', _isChildOf: 'outer-if', _stepPath: 'steps/0/then/0', _branchLabel: 'then' } } },
      { id: 'inner-if', type: 'block', position: { x: 250, y: 380 }, data: { blockType: 'if', label: 'If', props: { condition: 'port != "514"', _isChildOf: 'outer-if', _stepPath: 'steps/0/then/1', _branchLabel: 'then' } } },
      { id: 'inner-then', type: 'block', position: { x: 520, y: 500 }, data: { blockType: 'send', label: 'Send', props: { command: 'y', _isChildOf: 'inner-if', _stepPath: 'steps/0/then/1/then/0', _branchLabel: 'then' } } },
      { id: 'else-p', type: 'block', position: { x: 700, y: 260 }, data: { blockType: 'print', label: 'Print', props: { message: 'nope', _isChildOf: 'outer-if', _stepPath: 'steps/0/else/0', _branchLabel: 'else' } } },
      { id: 'tail', type: 'block', position: { x: 250, y: 700 }, data: { blockType: 'print', label: 'Print', props: { message: 'done', _stepPath: 'steps/1' } } },
      { id: 'dropped', type: 'block', position: { x: 60, y: 980 }, data: { blockType: 'print', label: 'Print', props: { message: 'foo' } } },
    ],
    edges: [
      { id: 'e-start', source: '__start__', target: 'outer-if' },
      { id: 'e-then', source: 'outer-if', target: 'then-a', label: 'then' },
      { id: 'e-a-inner', source: 'then-a', target: 'inner-if' },
      { id: 'e-inner-then', source: 'inner-if', target: 'inner-then', label: 'then' },
      { id: 'e-else', source: 'outer-if', sourceHandle: 'false', target: 'else-p', label: 'else' },
      { id: 'e-continue', source: 'outer-if', sourceHandle: 'continue', target: 'tail' },
    ],
  };
}

interface SnapNode { id: string; position: { x: number; y: number }; data?: { props?: Record<string, unknown> } }
async function nodeById(page: Page, id: string): Promise<SnapNode> {
  const snap = await getGraphSnapshot(page);
  return (snap.nodes as SnapNode[]).find((n) => n.id === id)!;
}
function propsOf(n: SnapNode): Record<string, unknown> {
  return (n.data?.props) ?? {};
}

test.describe('Flow Canvas — wiring a fresh block into a nested branch', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('a block wired to an inner IF\'s continue handle lands inside the band after Layout', async ({ page }) => {
    await setGraphViaActions(page, nestedFixture());
    await expect(page.locator('.react-flow__node[data-id="inner-if"]')).toBeVisible();
    await expect(page.locator('.react-flow__node[data-id="dropped"]')).toBeVisible();

    // The user wires the dropped block onto the inner IF's continue handle (the reported gesture).
    await connectViaActions(page, { source: 'inner-if', sourceHandle: 'continue', target: 'dropped' });

    // onConnect confers membership immediately: the block becomes the next THEN sibling of the inner IF.
    const droppedAfterConnect = await nodeById(page, 'dropped');
    expect(propsOf(droppedAfterConnect)._isChildOf).toBe('outer-if');
    expect(propsOf(droppedAfterConnect)._stepPath).toBe('steps/0/then/2');

    // Click Layout to reorganize (the step the user took that previously dumped the block bottom-left).
    await page.getByRole('button', { name: /Auto-organize|Layout/ }).click();

    const dropped = await nodeById(page, 'dropped');
    const thenA = await nodeById(page, 'then-a');
    const innerIf = await nodeById(page, 'inner-if');
    const tail = await nodeById(page, 'tail');

    // Inside the THEN lane: same column as the other THEN children, below the inner IF…
    expect(dropped.position.x).toBeCloseTo(thenA.position.x, 0);
    expect(dropped.position.y).toBeGreaterThan(innerIf.position.y);
    // …and crucially ABOVE the top-level tail — i.e. nested in the branch, NOT appended to the spine
    // bottom as an orphan (which is exactly where it landed before the fix).
    expect(dropped.position.y).toBeLessThan(tail.position.y);
  });
});

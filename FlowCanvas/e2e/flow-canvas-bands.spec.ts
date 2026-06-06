import { expect, test } from '@playwright/test';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

const messyIfElse = {
  nodes: [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', label: 'Start', props: {} } },
    { id: 'if-1', type: 'block', position: { x: 500, y: 500 }, data: { blockType: 'if', label: 'If', props: { condition: '${x}', _stepPath: 'steps/0' } } },
    { id: 'then-1', type: 'block', position: { x: 505, y: 505 }, data: { blockType: 'print', label: 'Then', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/0', _branchLabel: 'then', message: 't' } } },
    { id: 'else-1', type: 'block', position: { x: 510, y: 510 }, data: { blockType: 'print', label: 'Else', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/else/0', _branchLabel: 'else', message: 'e' } } },
  ],
  edges: [
    { id: 'edge-start-if', source: '__start__', target: 'if-1' },
    { id: 'edge-if-then', source: 'if-1', target: 'then-1', label: 'then' },
    { id: 'edge-if-else', source: 'if-1', target: 'else-1', sourceHandle: 'false', label: 'else' },
  ],
};

test.describe('Flow Canvas branch bands (labeled lanes)', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('renders a labeled THEN lane pill for an imported if/else', async ({ page }) => {
    await postHostMessage(page, { type: 'load-graph', ...messyIfElse });
    await expect(page.locator('.react-flow__node[data-id="if-1"]')).toBeVisible();
    const thenBand = page.locator('[data-testid="branch-band"][data-branch="then"]');
    await expect(thenBand).toHaveCount(1);
    // The label text now lives on the draggable handle (a sibling of the rectangle), not inside
    // the rectangle itself — the pill became its own pointer-grabbable element in the band-drag work.
    const thenHandle = page.locator('[data-testid="branch-band-handle"][data-branch="then"]');
    await expect(thenHandle).toContainText('THEN');
    const elseHandle = page.locator('[data-testid="branch-band-handle"][data-branch="else"]');
    await expect(elseHandle).toContainText('ELSE');
  });
});

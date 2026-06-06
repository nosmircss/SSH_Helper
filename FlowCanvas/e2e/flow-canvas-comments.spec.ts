import { expect, test } from '@playwright/test';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

const graphWithComment = {
  nodes: [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', label: 'Start', props: {} } },
    { id: 'n1', type: 'block', position: { x: 0, y: 120 }, data: { blockType: 'send', label: 'send', props: { command: 'hostname', _stepPath: 'steps/0' } } },
    { id: 'c1', type: 'comment', position: { x: 0, y: 0 }, data: { commentId: 'c1', blockType: 'comment', kind: 'comment', text: 'Get hostname', anchor: { type: 'leading', stepPath: 'steps/0' }, attachedToNodeId: 'n1' } },
  ],
  edges: [{ id: 'e0', source: '__start__', target: 'n1' }],
};

test.describe('Flow Canvas comments', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('imported comment renders as a pill', async ({ page }) => {
    await postHostMessage(page, { type: 'load-graph', ...graphWithComment });
    await expect(page.locator('[data-testid="comment-pill"]')).toHaveCount(1);
    await expect(page.locator('[data-testid="comment-pill"]')).toContainText('Get hostname');
  });
});

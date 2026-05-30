import { expect, test, type Locator, type Page } from '@playwright/test';
import { createInteractionFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

test.describe('Flow Canvas Run Timing', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createInteractionFixture());
    await expect(nodeById(page, 'node-1')).toBeVisible();
  });

  test('execution-update populates blockTimings and renders the duration badge', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 1500 });

    await expect(nodeById(page, 'node-1').getByText('1.5s', { exact: true })).toBeVisible();
  });

  test('sub-second durations render in milliseconds', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 250 });

    await expect(nodeById(page, 'node-1').getByText('250ms', { exact: true })).toBeVisible();
  });
});

function nodeById(page: Page, nodeId: string): Locator {
  return page.locator(`.react-flow__node[data-id="${nodeId}"]`);
}

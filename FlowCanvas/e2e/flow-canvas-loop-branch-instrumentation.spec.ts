import { expect, test, type Locator, type Page } from '@playwright/test';
import { createInteractionFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

const nodeById = (page: Page, id: string): Locator => page.locator(`.react-flow__node[data-id="${id}"]`);
const hasReducedMotion = (page: Page) =>
  page.evaluate(() => document.body.classList.contains('fc-reduced-motion'));

test.describe('Flow Canvas Loop & Branch Instrumentation', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createInteractionFixture());
    await expect(nodeById(page, 'node-1')).toBeVisible();
  });

  test('loop node shows the ×N iteration badge on completion', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 100, iterationCount: 5 });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveText('×5');
  });

  test('iterationCount 0 shows ×0', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 5, iterationCount: 0 });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveText('×0');
  });

  test('no loop badge when iterationCount absent', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 5 });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveCount(0);
  });

  test('branch node shows the derived label (else / case #3 / elif #1)', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 10, branchTaken: 'else' });
    await expect(nodeById(page, 'node-1').getByTestId('exec-branch-badge')).toHaveText('else');

    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 10, branchTaken: 'cases/2/do' });
    await expect(nodeById(page, 'node-1').getByTestId('exec-branch-badge')).toHaveText('case #3');

    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 10, branchTaken: 'elif/0/then' });
    await expect(nodeById(page, 'node-1').getByTestId('exec-branch-badge')).toHaveText('elif #1');
  });

  test('malformed instrumentation is ignored (no badge)', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 5, iterationCount: -1, branchTaken: '   ' });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveCount(0);
    await expect(nodeById(page, 'node-1').getByTestId('exec-branch-badge')).toHaveCount(0);
  });

  test('execution-started (re-run) clears the badge', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 5, iterationCount: 3 });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveText('×3');
    await postHostMessage(page, { type: 'execution-started' });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveCount(0);
  });

  test('badge renders identically under reduced motion (no motion added)', async ({ page }) => {
    await postHostMessage(page, { type: 'pref-restore', reducedMotion: true });
    await expect.poll(() => hasReducedMotion(page)).toBe(true);
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 5, iterationCount: 4 });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveText('×4');
  });
});

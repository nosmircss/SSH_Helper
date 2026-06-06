import { expect, test, type Locator, type Page } from '@playwright/test';
import { createInteractionFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  getGraphSnapshot,
  installHostMessageCapture,
  loadGraphFixture,
  openDisplaySettings,
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

  test('heatmap tints blocks by relative duration and reverts when toggled off', async ({ page }) => {
    await applyDurations(page);

    // Off by default — capture the no-heat baseline (success glow only). The
    // container has a 0.2s box-shadow transition, so reads must settle first.
    const baseline1 = await settledBoxShadow(page, 'node-1');
    await openDisplaySettings(page);
    const heatmapButton = page.getByRole('switch', { name: 'Heatmap' });

    await heatmapButton.click();
    const shadow1 = await settledBoxShadow(page, 'node-1');
    const shadow2 = await settledBoxShadow(page, 'node-2');
    const shadow3 = await settledBoxShadow(page, 'node-3');

    // Heatmap adds a 3px ring on top of the baseline, so it changes.
    expect(shadow1).not.toBe(baseline1);
    // Three distinct durations → three distinct heat tints.
    expect(shadow1).not.toBe(shadow2);
    expect(shadow2).not.toBe(shadow3);
    expect(shadow1).not.toBe(shadow3);

    // Toggling off reverts to the baseline.
    await heatmapButton.click();
    await expect.poll(() => boxShadowOf(page, 'node-1')).toBe(baseline1);
  });

  test('toggling heatmap emits layout-save and host restore re-activates the toolbar', async ({ page }) => {
    await clearOutgoingMessages(page);
    await openDisplaySettings(page);
    const heatmapSwitch = page.getByRole('switch', { name: 'Heatmap' });
    await heatmapSwitch.click();

    const saved = await waitForOutgoingMessage(page, 'layout-save');
    expect(saved.heatmapEnabled).toBe(true);

    // Turn it off, then prove an inbound restore re-activates the control (no echo needed).
    await heatmapSwitch.click();
    await postHostMessage(page, { type: 'layout-restore', heatmapEnabled: true });
    await expect(heatmapSwitch).toHaveAttribute('aria-checked', 'true');
  });

  test('PARITY: enabling heatmap is render-only and does not mutate the graph snapshot', async ({ page }) => {
    // Timings land first (Task 8 writes execState onto node.data); the heatmap toggle
    // must add nothing to the graph — capture the snapshot once timings are applied,
    // then prove the heatmap toggle leaves it byte-identical.
    await applyDurations(page);
    const before = await getGraphSnapshot(page);

    await openDisplaySettings(page);
    await page.getByRole('switch', { name: 'Heatmap' }).click();
    await expect(nodeById(page, 'node-1').getByText('100ms', { exact: true })).toBeVisible();

    const after = await getGraphSnapshot(page);
    expect(JSON.stringify(after)).toBe(JSON.stringify(before));
  });
});

async function applyDurations(page: Page): Promise<void> {
  const durations: Record<string, number> = { 'node-1': 100, 'node-2': 400, 'node-3': 900 };
  for (const [stepId, duration] of Object.entries(durations)) {
    await postHostMessage(page, { type: 'execution-update', stepId, state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId, state: 'success', duration });
  }
  await expect(nodeById(page, 'node-3').getByText('900ms', { exact: true })).toBeVisible();
}

async function boxShadowOf(page: Page, nodeId: string): Promise<string> {
  return nodeById(page, nodeId)
    .locator('> div')
    .first()
    .evaluate((el) => getComputedStyle(el as HTMLElement).boxShadow);
}

// The container animates box-shadow over 0.2s, so getComputedStyle catches
// interpolated mid-transition values. Poll until two reads agree, then return.
async function settledBoxShadow(page: Page, nodeId: string): Promise<string> {
  let previous = await boxShadowOf(page, nodeId);
  await expect
    .poll(async () => {
      const current = await boxShadowOf(page, nodeId);
      const stable = current === previous;
      previous = current;
      return stable;
    })
    .toBe(true);
  return previous;
}

function nodeById(page: Page, nodeId: string): Locator {
  return page.locator(`.react-flow__node[data-id="${nodeId}"]`);
}

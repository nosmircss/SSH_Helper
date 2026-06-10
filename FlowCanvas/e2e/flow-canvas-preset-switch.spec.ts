import { expect, test } from '@playwright/test';
import { installHostMessageCapture, postHostMessage, waitForOutgoingMessage } from './support/harness';

// Switching presets in an OPEN canvas must not leak per-preset view state across loads.
// Preset node ids collide (node-1, node-2, …), so a stale expandedNodes set from preset A
// makes preset B's blocks RENDER expanded while load-graph's auto-layout estimated them
// collapsed — blocks overlap until the canvas is closed and reopened (which starts clean).
test.describe('Flow Canvas preset switch state isolation', () => {
  const graph = (label: string, opts: { expanded?: boolean; disabled?: boolean } = {}) => ({
    type: 'load-graph',
    nodes: [
      { id: 'node-1', type: 'block', position: { x: 100, y: 80 }, data: {
        blockType: 'print', label: `${label}-1`, props: { message: 'a' },
        ...(opts.expanded ? { expanded: true } : {}), ...(opts.disabled ? { disabled: true } : {}) } },
      { id: 'node-2', type: 'block', position: { x: 100, y: 220 }, data: {
        blockType: 'send', label: `${label}-2`, props: { command: 'show ver', capture: 'out' },
        ...(opts.expanded ? { expanded: true } : {}) } },
    ],
    edges: [{ id: 'e1', source: 'node-1', target: 'node-2' }],
  });

  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
  });

  test('expansion does not leak from the previous preset onto id-colliding blocks', async ({ page }) => {
    await postHostMessage(page, graph('A', { expanded: true }));
    await expect(page.getByTestId('block-summary')).toHaveCount(2);

    // Preset B saved nothing expanded — its blocks must load collapsed (render matches the
    // collapsed height estimates the load-time auto-layout used).
    await postHostMessage(page, graph('B'));
    await expect(page.getByTestId('block-summary')).toHaveCount(0);

    // Switching back to a preset WITH saved expansion restores it from its own payload.
    await postHostMessage(page, graph('A', { expanded: true }));
    await expect(page.getByTestId('block-summary')).toHaveCount(2);
  });

  test('disabled state does not leak from the previous preset', async ({ page }) => {
    await postHostMessage(page, graph('A', { disabled: true }));
    await expect(page.locator('[data-testid="block-node"]', { hasText: 'DISABLED' })).toHaveCount(1);

    await postHostMessage(page, graph('B'));
    await expect(page.locator('[data-testid="block-node"]', { hasText: 'DISABLED' })).toHaveCount(0);
  });

  test('Default block state: Expanded applies immediately and forces every loaded preset open', async ({ page }) => {
    await postHostMessage(page, graph('A'));
    await expect(page.getByTestId('block-summary')).toHaveCount(0);

    await page.locator('button[title="Display settings"]').click();
    await page.getByTestId('setting-default-block-state').getByRole('button', { name: 'Expanded' }).click();
    await page.keyboard.press('Escape');
    // Applied to the open graph immediately…
    await expect(page.getByTestId('block-summary')).toHaveCount(2);

    // …and to a different preset with NOTHING expanded in its saved layout.
    await postHostMessage(page, graph('B'));
    await expect(page.getByTestId('block-summary')).toHaveCount(2);
  });

  test('fresh-open ordering: a settings restore arriving after load-graph applies the Expanded default', async ({ page }) => {
    await postHostMessage(page, graph('A'));
    await expect(page.getByTestId('block-summary')).toHaveCount(0);

    await postHostMessage(page, { type: 'layout-restore', defaultBlockExpanded: true });
    await expect(page.getByTestId('block-summary')).toHaveCount(2);
  });
});

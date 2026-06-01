import { expect, test } from '@playwright/test';
import { createRunParityFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

test.describe('Flow Canvas Problems Panel', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('apply-result diagnostics open the panel; clicking a row selects and centers the node', async ({ page }) => {
    await loadGraphFixture(page, createRunParityFixture());
    await expect(page.getByText('Beta', { exact: true })).toBeVisible();

    // Toggle the Problems panel on via the toolbar button.
    await page.getByRole('button', { name: /problems/i }).click();

    await postHostMessage(page, {
      type: 'apply-result',
      success: false,
      errors: ['Bad block'],
      warnings: [],
      diagnostics: [{ nodeId: 'node-2', severity: 'error', message: 'Bad block' }],
    });

    // Wait for the show-error round trip so we know the apply-result was processed.
    await waitForOutgoingMessage(page, 'show-error');

    // The panel header + row render the diagnostic.
    await expect(page.getByText('Problems (1)', { exact: true })).toBeVisible();
    const row = page.getByText('Bad block', { exact: true });
    await expect(row).toBeVisible();

    // Clicking the row selects node-2.
    await row.click();

    const node2 = page.locator('.react-flow__node[data-id="node-2"]');
    await expect(node2).toHaveClass(/selected/);

    // And brings node-2 near the viewport (its bounding box is on-screen).
    const box = await node2.boundingBox();
    expect(box).not.toBeNull();
    const viewport = page.viewportSize();
    expect(viewport).not.toBeNull();
    if (box && viewport) {
      expect(box.x).toBeGreaterThan(0);
      expect(box.y).toBeGreaterThan(0);
      expect(box.x).toBeLessThan(viewport.width);
      expect(box.y).toBeLessThan(viewport.height);
    }
  });
});

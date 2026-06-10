import { expect, test, type Page } from '@playwright/test';
import type { GraphFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages, installHostMessageCapture, loadGraphFixture, postHostMessage, waitForOutgoingMessage,
} from './support/harness';

function edgeFixture(stroke: string): GraphFixture {
  return {
    nodes: [
      { id: 'src', type: 'block', position: { x: 80, y: 120 }, data: { blockType: 'send', label: 'Send', props: {} } },
      { id: 'dst', type: 'block', position: { x: 420, y: 120 }, data: { blockType: 'print', label: 'Print', props: {} } },
    ],
    edges: [{ id: 'e1', source: 'src', target: 'dst', style: { stroke } }],
  };
}

async function resolveVar(page: Page, name: string): Promise<string> {
  return page.evaluate((n) => {
    const probe = document.createElement('div');
    probe.style.color = `var(${n})`;
    document.body.appendChild(probe);
    const v = getComputedStyle(probe).color;
    probe.remove();
    return v;
  }, name);
}

const edgePath = (page: Page) => page.locator('path#e1');
const lastStop = (page: Page) => page.locator('#fc-grad-e1 stop').last();

test.describe('Flow Canvas Live Wires', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('every edge renders a tokenized arrowhead marker', async ({ page }) => {
    await loadGraphFixture(page, edgeFixture('var(--fc-edge-idle)'));
    // Auto-layout stacks src/dst on the spine, so the edge is a purely-vertical (zero-width)
    // path that getBoundingClientRect reports as hidden. Assert DOM presence; the marker
    // token below is the actual subject of this test.
    await expect(edgePath(page)).toHaveCount(1);
    expect(await edgePath(page).getAttribute('marker-end')).toBe('url(#fc-arrow-idle)');
    await expect(page.locator('#fc-arrow-idle')).toHaveCount(1);
  });

  test('branch edge resolves to its branch token (then) on marker + gradient end', async ({ page }) => {
    await loadGraphFixture(page, edgeFixture('var(--fc-branch-then)'));
    expect(await edgePath(page).getAttribute('marker-end')).toBe('url(#fc-arrow-then)');
    const stopColor = await lastStop(page).evaluate((el) => getComputedStyle(el as Element).stopColor);
    expect(stopColor).toBe(await resolveVar(page, '--fc-branch-then'));
  });

  test('plain edge gradient end resolves to --fc-edge-idle', async ({ page }) => {
    await loadGraphFixture(page, edgeFixture('var(--fc-edge-idle)'));
    const stopColor = await lastStop(page).evaluate((el) => getComputedStyle(el as Element).stopColor);
    expect(stopColor).toBe(await resolveVar(page, '--fc-edge-idle'));
  });

  test('the frontier packet rides the edge whose target is running, then moves on', async ({ page }) => {
    await loadGraphFixture(page, edgeFixture('var(--fc-branch-then)'));
    const packet = page.locator('.fc-edge-packet');
    await expect(packet).toHaveCount(0); // at rest

    await postHostMessage(page, { type: 'execution-started' });
    // The packet is the live FRONTIER dot: it rides the edge whose TARGET block is running.
    // The source completing alone does not light it (that was the old, branch-confusing behavior).
    await postHostMessage(page, { type: 'execution-update', stepId: 'src', state: 'success' });
    await expect(packet).toHaveCount(0);

    await postHostMessage(page, { type: 'execution-update', stepId: 'dst', state: 'running' });
    await expect(packet).toHaveCount(1);
    expect(await packet.evaluate((el) => getComputedStyle(el as Element).animationName)).toContain('fc-packet-travel');

    // Frontier moves on: once the target completes, no dot is left behind (the neon on-path wire
    // — asserted in the execution-path spec — carries the built trail instead).
    await postHostMessage(page, { type: 'execution-update', stepId: 'dst', state: 'success' });
    await expect(packet).toHaveCount(0);
    await expect(edgePath(page)).toHaveClass(/fc-edge-onpath/);

    await postHostMessage(page, { type: 'execution-update', stepId: 'dst', state: 'running' }); // re-light to test reduced-motion
    await expect(packet).toHaveCount(1);
    await postHostMessage(page, { type: 'pref-restore', reducedMotion: true }); // enable reduced motion
    await expect(packet).toHaveCount(0);
  });
});

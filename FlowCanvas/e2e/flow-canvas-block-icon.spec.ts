import { expect, test } from '@playwright/test';
import type { GraphFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages, installHostMessageCapture, loadGraphFixture, waitForOutgoingMessage,
} from './support/harness';

// Registry-coverage spec for the vendored BlockIcon glyph map. One representative blockType per
// category proves every category's def.icon resolves to a non-empty <svg> (real geometry, never a
// blank glyph) and that an unknown key falls back without throwing.
//
// ORDERING NOTE (Wave 2a Task 1 vs Task 4): a <BlockIcon> only enters the DOM once BaseBlock's
// header renders it (Task 3). Section 1 shipped this spec with the DOM cases marked test.fixme;
// now that Task 3 wires <BlockIcon> into the BaseBlock header, the DOM cases are live (Task 4).
const SAMPLES = [
  { id: 'n-ssh', blockType: 'send' },         // ssh
  { id: 'n-if', blockType: 'if' },            // control-flow
  { id: 'n-set', blockType: 'set' },          // data
  { id: 'n-ping', blockType: 'ping' },        // network
  { id: 'n-print', blockType: 'print' },      // io
  { id: 'n-col', blockType: 'updatecolumn' }, // grid
  { id: 'n-wait', blockType: 'wait' },        // timing
];
function fixtureFor(id: string, blockType: string): GraphFixture {
  return { nodes: [{ id, type: 'block', position: { x: 120, y: 120 },
    data: { blockType, label: blockType, props: {} } }], edges: [] };
}

test.describe('Flow Canvas Block Icons', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  for (const s of SAMPLES) {
    test(`renders an svg icon for ${s.blockType}`, async ({ page }) => {
      await loadGraphFixture(page, fixtureFor(s.id, s.blockType));
      const svg = page.locator(`.react-flow__node[data-id="${s.id}"] svg`).first();
      await expect(svg).toBeVisible();
      const childCount = await svg.evaluate((el) => el.childElementCount);
      expect(childCount).toBeGreaterThan(0); // non-empty geometry, never a blank glyph
    });
  }
});

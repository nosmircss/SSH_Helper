import { expect, test } from '@playwright/test';
import type { GraphFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  waitForOutgoingMessage,
} from './support/harness';

// Authored default for a new comment. Mirrors DEFAULT_COMMENT_COLOR in src/utils/tokens.ts; the one
// raw hex permitted to appear in resolved styles (it is user data, not a token-layer color).
const DEFAULT_COMMENT_COLOR = '#e0c040';

function createSshBlockFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: 'node-ssh',
        type: 'block',
        position: { x: 140, y: 120 },
        data: {
          blockType: 'send',
          label: 'Send',
          props: { command: 'echo hello' },
        },
      },
    ],
    edges: [],
  };
}

test.describe('Flow Canvas Token Sweep', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('root exposes OKLCH accent token', async ({ page }) => {
    const accent = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--fc-accent').trim(),
    );
    expect(accent).not.toBe('');
    expect(accent).toContain('oklch');
  });

  test('ssh block border resolves to its category token', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());

    const block = page.locator('.react-flow__node[data-id="node-ssh"]');
    await expect(block).toBeVisible();

    // The BaseBlock container (the bordered element) is the node's first child div.
    const container = block.locator('> div').first();

    const renderedBorder = await container.evaluate(
      (el) => getComputedStyle(el as HTMLElement).borderTopColor,
    );
    const tokenBorder = await page.evaluate(() => {
      // Resolve the var by reading the same computed property the block consumes.
      const probe = document.createElement('div');
      probe.style.color = 'var(--fc-cat-ssh-border)';
      document.body.appendChild(probe);
      const value = getComputedStyle(probe).color;
      probe.remove();
      return value;
    });

    expect(renderedBorder).not.toBe('');
    expect(renderedBorder).toBe(tokenBorder);
  });

  // The no-raw-hex sweep is incomplete until Task 5 finishes the panel sweep, so this CI gate
  // stays disabled until Task 5 Step 4 un-skips it.
  test.skip('no raw hex outside the token layer', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    await expect(page.locator('.react-flow__node[data-id="node-ssh"]')).toBeVisible();

    const offenders = await page.evaluate((commentColor) => {
      const hexRe = /#[0-9a-fA-F]{3,8}\b/;
      const normalize = (s: string) => s.toLowerCase();
      const allow = normalize(commentColor);
      const hits: string[] = [];
      for (const el of Array.from(document.querySelectorAll<HTMLElement>('[style]'))) {
        const style = el.getAttribute('style') ?? '';
        const matches = style.match(new RegExp(hexRe, 'g'));
        if (!matches) continue;
        for (const m of matches) {
          if (normalize(m) === allow) continue;
          hits.push(`${el.tagName.toLowerCase()}: ${m}`);
        }
      }
      return hits;
    }, DEFAULT_COMMENT_COLOR);

    expect(offenders, offenders.join('\n')).toEqual([]);
  });
});

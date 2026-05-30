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

  // The panel sweep is complete (Task 5); this CI gate enforces Decision #4 — no raw hex in any
  // resolved inline style except the one user-data comment color (DEFAULT_COMMENT_COLOR).
  //
  // Two complementary scans run together because each catches a different failure mode:
  //   (1) a raw '#'-hex literal (a token that never made it into the token layer), and
  //   (2) a malformed `var(--x)<hex-alpha>` concatenation — the regression class from the
  //       hex→var() sweep, where `color + '55'` silently became invalid CSS like
  //       `var(--fc-accent)55`. The browser DROPS such a declaration, so scan (1) alone is
  //       blind to it (no '#'). The Toolbar and Palette render by default in App, so loading
  //       any fixture mounts the exact controls (btnStyle / PaletteItem borders) where the bug
  //       lived — reverting fix #1 makes scan (2) fail here.
  test('no raw hex outside the token layer', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    await expect(page.locator('.react-flow__node[data-id="node-ssh"]')).toBeVisible();
    // Confirm the toolbar + palette shell is present, so their inline styles are in the scan
    // (these are where the var()+hex-alpha regression lived).
    await expect(page.getByText('Flow Canvas v2')).toBeVisible(); // toolbar
    await expect(page.getByText('Blocks', { exact: true })).toBeVisible(); // palette header

    const { hexOffenders, malformedOffenders } = await page.evaluate((commentColor) => {
      const hexRe = /#[0-9a-fA-F]{3,8}\b/g;
      // A var() reference immediately followed by hex-alpha chars (no separator) — i.e. the
      // dead `color + '55'` idiom applied to a var() string.
      const malformedRe = /var\([^)]*\)[0-9a-fA-F]{2,8}\b/g;
      const normalize = (s: string) => s.toLowerCase();
      const allow = normalize(commentColor);
      const hexHits: string[] = [];
      const malformedHits: string[] = [];
      for (const el of Array.from(document.querySelectorAll<HTMLElement>('[style]'))) {
        const style = el.getAttribute('style') ?? '';
        const tag = el.tagName.toLowerCase();
        for (const m of style.match(hexRe) ?? []) {
          if (normalize(m) === allow) continue;
          hexHits.push(`${tag}: ${m}`);
        }
        for (const m of style.match(malformedRe) ?? []) {
          malformedHits.push(`${tag}: ${m}`);
        }
      }
      return { hexOffenders: hexHits, malformedOffenders: malformedHits };
    }, DEFAULT_COMMENT_COLOR);

    expect(hexOffenders, hexOffenders.join('\n')).toEqual([]);
    expect(malformedOffenders, malformedOffenders.join('\n')).toEqual([]);
  });
});

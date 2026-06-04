import { expect, test, type Locator, type Page } from '@playwright/test';
import { createInteractionFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  getOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  setGraphViaActions,
  waitForOutgoingMessage,
} from './support/harness';

test.describe('Flow Canvas Interaction Correctness', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createInteractionFixture());
    await expect(nodeById(page, 'node-1')).toBeVisible();
    await expect(nodeById(page, 'node-2')).toBeVisible();
    await expect(nodeById(page, 'node-3')).toBeVisible();
  });

  test('node drag undo restores original position', async ({ page }) => {
    const node = nodeById(page, 'node-1');
    const before = await getNodeTranslate(page, 'node-1');

    await dragNodeBy(node, page, 140, 90);
    const afterDrag = await getNodeTranslate(page, 'node-1');
    expect(afterDrag).not.toEqual(before);

    await page.keyboard.press('Control+Z');
    await expect.poll(async () => getNodeTranslate(page, 'node-1')).toEqual(before);
  });

  test('right-click opens context menu without toggling breakpoint', async ({ page }) => {
    const node = nodeById(page, 'node-1');
    await node.click({ button: 'right' });

    await expect(page.getByText('Toggle Breakpoint', { exact: true })).toBeVisible();

    const outgoing = await getOutgoingMessages(page);
    expect(outgoing.some((m) => m.type === 'breakpoint-toggle')).toBeFalsy();

    await page.getByText('Toggle Breakpoint', { exact: true }).click();
    await waitForOutgoingMessage(page, 'breakpoint-toggle');
  });

  test('add comment is persistent through undo and redo', async ({ page }) => {
    await nodeById(page, 'node-2').click({ button: 'right' });
    await page.getByText('Add Comment (#)', { exact: true }).click();

    await expect(commentNodes(page)).toHaveCount(1);

    await page.keyboard.press('Control+Z');
    await expect(commentNodes(page)).toHaveCount(0);

    await page.keyboard.press('Control+Y');
    await expect(commentNodes(page)).toHaveCount(1);
  });

  test('box select sync removes selected nodes via Delete', async ({ page }) => {
    const one = await nodeById(page, 'node-1').boundingBox();
    const two = await nodeById(page, 'node-2').boundingBox();

    if (!one || !two) throw new Error('Expected node bounding boxes for selection test.');

    const startX = Math.min(one.x, two.x) - 30;
    const startY = Math.min(one.y, two.y) - 30;
    const endX = Math.max(one.x + one.width, two.x + two.width) + 30;
    const endY = Math.max(one.y + one.height, two.y + two.height) + 30;

    await page.mouse.move(startX, startY);
    await page.mouse.down();
    await page.mouse.move(endX, endY, { steps: 12 });
    await page.mouse.up();

    await expect(nodeById(page, 'node-1')).toHaveClass(/selected/);
    await expect(nodeById(page, 'node-2')).toHaveClass(/selected/);

    await page.keyboard.press('Delete');

    await expect(nodeById(page, 'node-1')).toHaveCount(0);
    await expect(nodeById(page, 'node-2')).toHaveCount(0);
    await expect(nodeById(page, 'node-3')).toHaveCount(1);
  });

  test('apply yaml payload omits schema default props', async ({ page }) => {
    await setGraphViaActions(page, createDefaultStrippingFixture());
    await clearOutgoingMessages(page);

    await page.getByRole('button', { name: /apply yaml/i }).click();
    const applyMessage = await waitForOutgoingMessage(page, 'apply-yaml');
    const nodes = toRecordArray(applyMessage.nodes);

    const sendNode = nodes.find((node) => node.id === 'send-defaults');
    expect(sendNode).toBeTruthy();
    expect(getNodeProps(sendNode)).toEqual({
      command: 'show version',
    });

    const interactiveNode = nodes.find((node) => node.id === 'interactive-mixed');
    expect(interactiveNode).toBeTruthy();
    expect(getNodeProps(interactiveNode)).toEqual({
      command: 'show interface status',
      show_window: false,
    });
  });
});

function toRecordArray(value: unknown): Array<Record<string, unknown>> {
  if (!Array.isArray(value)) return [];
  return value.filter((item): item is Record<string, unknown> => !!item && typeof item === 'object');
}

function getNodeProps(node: Record<string, unknown> | undefined): Record<string, unknown> {
  const data = node?.data;
  if (!data || typeof data !== 'object') {
    return {};
  }

  const props = (data as Record<string, unknown>).props;
  return props && typeof props === 'object' ? props as Record<string, unknown> : {};
}

function createDefaultStrippingFixture(): { nodes: Array<Record<string, unknown>>; edges: Array<Record<string, unknown>> } {
  return {
    nodes: [
      {
        id: '__start__',
        type: 'start',
        position: { x: 80, y: 40 },
        data: {
          blockType: '_start',
          label: 'Untitled Script',
          props: {},
        },
      },
      {
        id: 'send-defaults',
        type: 'block',
        position: { x: 80, y: 160 },
        data: {
          blockType: 'send',
          label: 'Send',
          props: {
            command: 'show version',
            suppress: false,
            retry: 0,
            retry_delay: 1,
            fail_on_nonzero: false,
            on_error: 'stop',
          },
        },
      },
      {
        id: 'interactive-mixed',
        type: 'block',
        position: { x: 80, y: 280 },
        data: {
          blockType: 'interactive',
          label: 'Interactive',
          props: {
            session: 'separate',
            mirror_output: false,
            show_window: false,
            on_error: 'stop',
            command: 'show interface status',
          },
        },
      },
    ],
    edges: [
      {
        id: 'edge-start-send-defaults',
        source: '__start__',
        target: 'send-defaults',
        style: { stroke: '#666' },
      },
      {
        id: 'edge-send-defaults-interactive-mixed',
        source: 'send-defaults',
        target: 'interactive-mixed',
        style: { stroke: '#666' },
      },
    ],
  };
}

function nodeById(page: Page, nodeId: string): Locator {
  return page.locator(`.react-flow__node[data-id="${nodeId}"]`);
}

function commentNodes(page: Page): Locator {
  return page.locator('.react-flow__node-comment');
}

async function dragNodeBy(node: Locator, page: Page, deltaX: number, deltaY: number): Promise<void> {
  const box = await node.boundingBox();
  if (!box) throw new Error('Expected node bounding box for drag operation.');

  const fromX = box.x + box.width / 2;
  const fromY = box.y + box.height / 2;

  await page.mouse.move(fromX, fromY);
  await page.mouse.down();
  await page.mouse.move(fromX + deltaX, fromY + deltaY, { steps: 15 });
  await page.mouse.up();
}

async function getNodeTranslate(page: Page, nodeId: string): Promise<{ x: number; y: number }> {
  return page.evaluate((id) => {
    const el = document.querySelector(`.react-flow__node[data-id="${id}"]`) as HTMLElement | null;
    if (!el) throw new Error(`Node '${id}' not found.`);

    const transform = el.style.transform || '';
    const match = transform.match(/translate\(([-\d.]+)px,\s*([-\d.]+)px\)/);
    if (!match) throw new Error(`Unable to parse transform '${transform}' for node '${id}'.`);

    return {
      x: Number(match[1]),
      y: Number(match[2]),
    };
  }, nodeId);
}

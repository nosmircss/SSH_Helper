import { expect, type Page } from '@playwright/test';
import type { GraphFixture } from '../fixtures/graphs';

export interface OutgoingHostMessage {
  type: string;
  [key: string]: unknown;
}

export async function installHostMessageCapture(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const globalWindow = window as Window & {
      __flowCanvasOutgoing?: unknown[];
      __FLOWCANVAS_TEST_HOOKS__?: {
        onOutgoingMessage?: (msg: unknown) => void;
      };
    };

    globalWindow.__flowCanvasOutgoing = [];
    globalWindow.__FLOWCANVAS_TEST_HOOKS__ = {
      onOutgoingMessage: (msg: unknown) => {
        // Structured clone to avoid reactive object references.
        const cloned = JSON.parse(JSON.stringify(msg));
        globalWindow.__flowCanvasOutgoing?.push(cloned);
      },
    };
  });
}

export async function getOutgoingMessages(page: Page): Promise<OutgoingHostMessage[]> {
  return page.evaluate(() => {
    const globalWindow = window as Window & { __flowCanvasOutgoing?: OutgoingHostMessage[] };
    return [...(globalWindow.__flowCanvasOutgoing ?? [])];
  });
}

export async function clearOutgoingMessages(page: Page): Promise<void> {
  await page.evaluate(() => {
    const globalWindow = window as Window & { __flowCanvasOutgoing?: unknown[] };
    globalWindow.__flowCanvasOutgoing = [];
  });
}

export async function waitForOutgoingMessage(
  page: Page,
  messageType: string,
): Promise<OutgoingHostMessage> {
  await expect
    .poll(async () => {
      const messages = await getOutgoingMessages(page);
      return messages.some((m) => m.type === messageType);
    })
    .toBeTruthy();

  const messages = await getOutgoingMessages(page);
  const match = messages.find((m) => m.type === messageType);
  if (!match) {
    throw new Error(`Expected outgoing message '${messageType}' but none were captured.`);
  }

  return match;
}

export async function postHostMessage(page: Page, message: Record<string, unknown>): Promise<void> {
  await page.evaluate((payload) => {
    window.postMessage(payload, '*');
  }, message);
}

export async function loadGraphFixture(page: Page, fixture: GraphFixture): Promise<void> {
  await postHostMessage(page, {
    type: 'load-graph',
    nodes: fixture.nodes,
    edges: fixture.edges,
  });
}

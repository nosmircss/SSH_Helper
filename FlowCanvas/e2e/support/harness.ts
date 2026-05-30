import { expect, type Page } from '@playwright/test';
import type { GraphFixture } from '../fixtures/graphs';

export interface OutgoingHostMessage {
  type: string;
  [key: string]: unknown;
}

interface RuleConnection {
  source: string | null;
  target: string | null;
  sourceHandle?: string | null;
  targetHandle?: string | null;
}

interface FlowCanvasTestHooks {
  onOutgoingMessage?: (msg: unknown) => void;
  setGraphViaActions?: (graph: GraphFixture) => void;
  clearGraphViaActions?: () => void;
  getGraphSnapshot?: () => GraphFixture;
  connectViaActions?: (conn: RuleConnection) => void;
}

export async function installHostMessageCapture(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const globalWindow = window as Window & {
      __flowCanvasOutgoing?: unknown[];
      __FLOWCANVAS_TEST_HOOKS__?: FlowCanvasTestHooks;
    };

    globalWindow.__flowCanvasOutgoing = [];
    const existingHooks = globalWindow.__FLOWCANVAS_TEST_HOOKS__ ?? {};
    globalWindow.__FLOWCANVAS_TEST_HOOKS__ = {
      ...existingHooks,
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

export async function setGraphViaActions(page: Page, fixture: GraphFixture): Promise<void> {
  await page.evaluate((payload) => {
    const globalWindow = window as Window & { __FLOWCANVAS_TEST_HOOKS__?: FlowCanvasTestHooks };
    const setGraph = globalWindow.__FLOWCANVAS_TEST_HOOKS__?.setGraphViaActions;
    if (typeof setGraph !== 'function') {
      throw new Error('Missing test hook setGraphViaActions. Ensure Flow Canvas test hooks are installed.');
    }

    setGraph(payload);
  }, fixture);
}

export async function clearGraphViaActions(page: Page): Promise<void> {
  await page.evaluate(() => {
    const globalWindow = window as Window & { __FLOWCANVAS_TEST_HOOKS__?: FlowCanvasTestHooks };
    const clearGraph = globalWindow.__FLOWCANVAS_TEST_HOOKS__?.clearGraphViaActions;
    if (typeof clearGraph !== 'function') {
      throw new Error('Missing test hook clearGraphViaActions. Ensure Flow Canvas test hooks are installed.');
    }

    clearGraph();
  });
}

export async function connectViaActions(
  page: Page,
  connection: { source: string; target: string; sourceHandle?: string | null; targetHandle?: string | null },
): Promise<void> {
  await page.evaluate((conn) => {
    const globalWindow = window as Window & { __FLOWCANVAS_TEST_HOOKS__?: FlowCanvasTestHooks };
    const connect = globalWindow.__FLOWCANVAS_TEST_HOOKS__?.connectViaActions;
    if (typeof connect !== 'function') {
      throw new Error('Missing test hook connectViaActions. Ensure Flow Canvas test hooks are installed.');
    }

    connect(conn);
  }, connection);
}

export async function getGraphSnapshot(page: Page): Promise<GraphFixture> {
  return page.evaluate(() => {
    const globalWindow = window as Window & { __FLOWCANVAS_TEST_HOOKS__?: FlowCanvasTestHooks };
    const getGraph = globalWindow.__FLOWCANVAS_TEST_HOOKS__?.getGraphSnapshot;
    if (typeof getGraph !== 'function') {
      throw new Error('Missing test hook getGraphSnapshot. Ensure Flow Canvas test hooks are installed.');
    }

    return getGraph();
  });
}

/**
 * MessageBus handles bidirectional communication between the React app
 * and the WinForms host via WebView2's PostWebMessage API.
 *
 * WinForms → React: window.chrome.webview.addEventListener('message', ...)
 * React → WinForms: window.chrome.webview.postMessage(...)
 */

export interface BridgeMessage {
  type: string;
  [key: string]: unknown;
}

type MessageHandler = (msg: BridgeMessage) => void;

class MessageBusImpl {
  private handlers = new Map<string, Set<MessageHandler>>();
  private ready = false;
  private isWebView2 = false;

  constructor() {
    // Detect WebView2 environment
    this.isWebView2 = !!(window as any).chrome?.webview;

    if (this.isWebView2) {
      (window as any).chrome.webview.addEventListener('message', (event: MessageEvent) => {
        try {
          const msg: BridgeMessage = typeof event.data === 'string'
            ? JSON.parse(event.data)
            : event.data;
          this.dispatch(msg);
        } catch (e) {
          console.error('[MessageBus] Failed to parse message:', e);
        }
      });
    }

    // Also listen for window messages (useful for dev/testing without WebView2)
    window.addEventListener('message', (event: MessageEvent) => {
      if (event.source !== window) return;
      try {
        const msg: BridgeMessage = typeof event.data === 'string'
          ? JSON.parse(event.data)
          : event.data;
        if (msg.type) this.dispatch(msg);
      } catch {
        // Ignore non-JSON messages
      }
    });
  }

  /** Subscribe to messages of a specific type. */
  on(type: string, handler: MessageHandler): () => void {
    if (!this.handlers.has(type)) {
      this.handlers.set(type, new Set());
    }
    this.handlers.get(type)!.add(handler);
    return () => this.handlers.get(type)?.delete(handler);
  }

  /** Send a message to WinForms (or to window for dev testing). */
  send(msg: BridgeMessage): void {
    if (this.isWebView2) {
      (window as any).chrome.webview.postMessage(msg);
    } else {
      console.log('[MessageBus → Host]', msg);
    }
  }

  /** Signal that the React app is ready to receive messages. */
  sendReady(): void {
    if (this.ready) return;
    this.ready = true;
    this.send({ type: 'ready' });
  }

  private dispatch(msg: BridgeMessage): void {
    const handlers = this.handlers.get(msg.type);
    if (handlers) {
      handlers.forEach((h) => {
        try {
          h(msg);
        } catch (e) {
          console.error(`[MessageBus] Handler error for '${msg.type}':`, e);
        }
      });
    }
  }
}

export const messageBus = new MessageBusImpl();

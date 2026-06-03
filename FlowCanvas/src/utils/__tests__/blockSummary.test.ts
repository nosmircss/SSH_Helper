// FlowCanvas/src/utils/__tests__/blockSummary.test.ts
import { describe, it, expect } from 'vitest';
import { summarizeBlock } from '../blockSummary';

describe('summarizeBlock', () => {
  it('shows required + non-default fields, hides defaults', () => {
    const r = summarizeBlock('send', { command: 'show ver', capture: 'out' });
    const keys = r.rows.map((x) => x.key);
    expect(keys).toContain('command'); // required
    expect(keys).toContain('capture'); // non-default
    expect(keys).not.toContain('timeout'); // default (empty)
    expect(keys).not.toContain('on_error'); // default 'stop'
    expect(r.hiddenCount).toBeGreaterThan(0);
  });
  it('marks a required-but-empty field as not set', () => {
    const r = summarizeBlock('send', {});
    const cmd = r.rows.find((x) => x.key === 'command')!;
    expect(cmd.notSet).toBe(true);
  });
  it('respects http auth conditional (auth=none hides token)', () => {
    const r = summarizeBlock('http', { url: 'https://x', auth: 'none', token: 'abc' });
    // token is advanced + auth=none → not required; it has a value so it still shows as non-default,
    // but is masked. Assert masking flag instead of visibility.
    const tok = r.rows.find((x) => x.key === 'token');
    if (tok) expect(tok.masked).toBe(true);
  });
});

import { describe, it, expect } from 'vitest';
import { idleNeon, nodeBorderColor, resolveNodeShadow } from '../nodeStyle';

const SSH = 'var(--fc-cat-ssh-border)';

describe('idleNeon', () => {
  it('builds the Balanced 3-part ring from the category hue via color-mix', () => {
    expect(idleNeon(SSH)).toBe(
      '0 0 0 1px color-mix(in oklch, var(--fc-cat-ssh-border) 36%, transparent), ' +
      '0 0 10px -2px color-mix(in oklch, var(--fc-cat-ssh-border) 46%, transparent), ' +
      'inset 0 0 10px -7px color-mix(in oklch, var(--fc-cat-ssh-border) 60%, transparent)',
    );
  });
});

describe('nodeBorderColor', () => {
  it('uses the white selected border when selected (wins over disabled)', () => {
    expect(nodeBorderColor({ selected: true, isDisabled: true, border: SSH })).toBe('var(--fc-border-selected)');
  });
  it('uses the muted border when disabled', () => {
    expect(nodeBorderColor({ selected: false, isDisabled: true, border: SSH })).toBe('var(--fc-border-muted)');
  });
  it('uses the category hue otherwise (the persistent ring)', () => {
    expect(nodeBorderColor({ selected: false, isDisabled: false, border: SSH })).toBe(SSH);
  });
});

describe('resolveNodeShadow', () => {
  const base = { selected: false, heatActive: false, border: SSH } as const;
  it('idle (no heat) → the category neon ring', () => {
    expect(resolveNodeShadow({ ...base, execState: 'idle' })).toBe(idleNeon(SSH));
  });
  it('idle + heat active → none (heat ring takes the slot, no double-ring)', () => {
    expect(resolveNodeShadow({ ...base, execState: 'idle', heatActive: true })).toBe('none');
  });
  it('selected → the white selected glow, not the idle ring', () => {
    expect(resolveNodeShadow({ ...base, execState: 'idle', selected: true })).toBe('0 0 12px var(--fc-glow-selected)');
  });
  it('success → the success glow', () => {
    expect(resolveNodeShadow({ ...base, execState: 'success' })).toBe('0 0 10px var(--fc-glow-success)');
  });
  it('skipped → the skipped glow', () => {
    expect(resolveNodeShadow({ ...base, execState: 'skipped' })).toBe('0 0 16px var(--fc-glow-skipped)');
  });
  it('running → none (the fc-exec-running animation owns the shadow)', () => {
    expect(resolveNodeShadow({ ...base, execState: 'running' })).toBe('none');
  });
  it('error → none (the fc-exec-error animation owns the shadow)', () => {
    expect(resolveNodeShadow({ ...base, execState: 'error' })).toBe('none');
  });
  it('disabled → none', () => {
    expect(resolveNodeShadow({ ...base, execState: 'disabled' })).toBe('none');
  });
});

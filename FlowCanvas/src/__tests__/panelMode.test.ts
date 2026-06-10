import { describe, it, expect } from 'vitest';
import { panelFromSearch } from '../panelMode';

describe('panelFromSearch', () => {
  it('returns "runoutput" when panel=runoutput', () => {
    expect(panelFromSearch('?panel=runoutput')).toBe('runoutput');
    expect(panelFromSearch('?foo=bar&panel=runoutput')).toBe('runoutput');
  });
  it('returns "main" otherwise', () => {
    expect(panelFromSearch('')).toBe('main');
    expect(panelFromSearch('?panel=other')).toBe('main');
    expect(panelFromSearch('?x=1')).toBe('main');
  });
});

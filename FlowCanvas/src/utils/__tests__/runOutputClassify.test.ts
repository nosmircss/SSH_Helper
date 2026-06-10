import { describe, it, expect } from 'vitest';
import { classifyRunOutputLine } from '../runOutputClassify';

describe('classifyRunOutputLine', () => {
  it('classifies the ### banner delimiter as banner', () => {
    expect(classifyRunOutputLine('############### CONNECTED TO 10.0.0.5 admin@fw> ###############')).toBe('banner');
  });

  it('classifies obvious error lines as error', () => {
    expect(classifyRunOutputLine('command parse error before \'sytsem\'')).toBe('error');
    expect(classifyRunOutputLine('Command fail. Return code -61')).toBe('error');
    expect(classifyRunOutputLine('% Invalid input detected')).toBe('error');
    expect(classifyRunOutputLine('Permission denied')).toBe('error');
  });

  it('classifies plain output as normal', () => {
    expect(classifyRunOutputLine('Version: FortiGate-100F v7.4.3')).toBe('normal');
    expect(classifyRunOutputLine('Uptime: 47 days')).toBe('normal');
  });

  it('does not treat a short hash comment as a banner', () => {
    expect(classifyRunOutputLine('# a comment')).toBe('normal');
  });
});

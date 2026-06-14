'use strict';

const { renderContract, renderManagedBlock, extractManagedBlock } = require('../src/renderer');
const { BASE_CONFIG } = require('../src/base-config');

describe('renderContract', () => {
  test('includes mode, sections, anchor regex, sentinel', () => {
    const out = renderContract(BASE_CONFIG);
    expect(out).toContain('strict');
    expect(out).toContain('Overview');
    expect(out).toContain('Behavior');
    expect(out).toContain('Rejected and Reverted');
    expect(out).toContain('Change Log');
    expect(out).toContain('- None on record.');
  });
});

describe('renderManagedBlock', () => {
  test('wraps contract in managed block markers', () => {
    const block = renderManagedBlock(BASE_CONFIG);
    expect(block).toContain('<!-- spec-lint:start -->');
    expect(block).toContain('<!-- spec-lint:end -->');
  });
});

describe('extractManagedBlock', () => {
  test('extracts block from readme', () => {
    const readme = `# Specs\n\n<!-- spec-lint:start -->\nsome content\n<!-- spec-lint:end -->\n\nMore.`;
    expect(extractManagedBlock(readme)).toBe('some content');
  });

  test('returns null when no markers', () => {
    expect(extractManagedBlock('# No markers')).toBeNull();
  });
});

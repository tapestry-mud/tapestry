'use strict';

const path = require('path');
const { formatResults } = require('../src/formatter');

const specsDir = '/project/specs';
const noViolations = {
  fileResults: { '/project/specs/feature-a.md': [] },
  crossFileViolations: [],
};
const withViolations = {
  fileResults: { '/project/specs/feature-a.md': [{ rule: 'frontmatter', detail: 'missing capability' }] },
  crossFileViolations: [{ rule: 'index-sync', detail: 'ghost.md not on disk' }],
};

describe('formatResults', () => {
  test('shows OK for clean files', () => {
    const out = formatResults(specsDir, noViolations, 'strict');
    expect(out).toContain('OK  feature-a.md');
    expect(out).toContain('1 files, 0 violations');
  });

  test('shows FAIL in strict mode', () => {
    const out = formatResults(specsDir, withViolations, 'strict');
    expect(out).toContain('FAIL');
    expect(out).toContain('frontmatter');
    expect(out).toContain('2 failures');
  });

  test('shows WARN in lenient mode', () => {
    const out = formatResults(specsDir, withViolations, 'lenient');
    expect(out).toContain('WARN');
    expect(out).not.toContain('FAIL');
    expect(out).toContain('lenient: not blocking');
  });
});

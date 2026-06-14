'use strict';

const checkChangelog = require('../../src/checks/changelog');
const { BASE_CONFIG } = require('../../src/base-config');

function make(clContent) {
  return `---
capability: feature-a
last-updated: 2026-01-15
---

## Change Log

${clContent}`;
}

describe('checkChangelog', () => {
  test('passes on empty Change Log', () => {
    expect(checkChangelog('feature-a.md', make(''), BASE_CONFIG)).toHaveLength(0);
  });

  test('passes on valid newest-first list', () => {
    const content = make(`- 2026-01-15 [feature-a-v2](changes/2026-01-15-v2.md)
- 2026-01-01 [feature-a-v1](changes/2026-01-01-v1.md)`);
    expect(checkChangelog('feature-a.md', content, BASE_CONFIG)).toHaveLength(0);
  });

  test('fails when Change Log is a markdown table', () => {
    const content = make(`| Date | Record |
|------|--------|
| 2026-01-15 | v1 |`);
    const v = checkChangelog('feature-a.md', content, BASE_CONFIG);
    expect(v.length).toBeGreaterThan(0);
    expect(v[0].rule).toBe('changelog');
  });

  test('fails when entries are not newest-first', () => {
    const content = make(`- 2026-01-01 [v1](changes/2026-01-01-v1.md)
- 2026-01-15 [v2](changes/2026-01-15-v2.md)`);
    const v = checkChangelog('feature-a.md', content, BASE_CONFIG);
    expect(v.length).toBeGreaterThan(0);
    expect(v[0].detail).toMatch(/newest-first/);
  });
});

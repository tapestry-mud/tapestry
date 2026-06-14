'use strict';

const checkSentinel = require('../../src/checks/sentinel');
const { BASE_CONFIG } = require('../../src/base-config');

function make(rrContent) {
  return `---
capability: feature-a
last-updated: 2026-01-15
---

## Rejected and Reverted

${rrContent}

## Change Log
`;
}

describe('checkSentinel', () => {
  test('passes when empty R&R has exact sentinel', () => {
    expect(checkSentinel('feature-a.md', make('- None on record.'), BASE_CONFIG)).toHaveLength(0);
  });

  test('fails when empty R&R is blank', () => {
    const v = checkSentinel('feature-a.md', make(''), BASE_CONFIG);
    expect(v.length).toBeGreaterThan(0);
    expect(v[0].rule).toBe('sentinel');
  });

  test('fails when empty R&R has prose instead of sentinel', () => {
    const v = checkSentinel('feature-a.md', make('No reversals so far.'), BASE_CONFIG);
    expect(v.length).toBeGreaterThan(0);
  });

  test('passes when R&R has real entries (sentinel not required)', () => {
    const content = make('- Rejected idea X because Y.');
    expect(checkSentinel('feature-a.md', content, BASE_CONFIG)).toHaveLength(0);
  });

  test('returns no violations when R&R section is missing (check 2 owns that)', () => {
    const noRR = `---
capability: feature-a
last-updated: 2026-01-15
---

## Overview

.
`;
    expect(checkSentinel('feature-a.md', noRR, BASE_CONFIG)).toHaveLength(0);
  });
});

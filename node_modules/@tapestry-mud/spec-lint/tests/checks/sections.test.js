'use strict';

const checkSections = require('../../src/checks/sections');
const { BASE_CONFIG } = require('../../src/base-config');

const ALL_SECTIONS = `---
capability: feature-a
last-updated: 2026-01-15
---

# Feature A

## Overview

Overview text.

## Behavior

- Works. (src/a.js:1)

## Rejected and Reverted

- None on record.

## Change Log
`;

const MISSING_BEHAVIOR = `---
capability: feature-a
last-updated: 2026-01-15
---

# Feature A

## Overview

Overview text.

## Rejected and Reverted

- None on record.

## Change Log
`;

describe('checkSections', () => {
  test('returns no violations when all four sections present', () => {
    expect(checkSections('feature-a.md', ALL_SECTIONS, BASE_CONFIG)).toHaveLength(0);
  });

  test('reports missing section by name', () => {
    const v = checkSections('feature-a.md', MISSING_BEHAVIOR, BASE_CONFIG);
    expect(v.length).toBeGreaterThan(0);
    expect(v[0].rule).toBe('sections');
    expect(v[0].detail).toContain('Behavior');
  });

  test('respects extra sections from config', () => {
    const cfg = { ...BASE_CONFIG, sections: [...BASE_CONFIG.sections, 'Security'] };
    const v = checkSections('feature-a.md', ALL_SECTIONS, cfg);
    expect(v.some(vio => vio.detail.includes('Security'))).toBe(true);
  });
});

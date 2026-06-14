'use strict';

const path = require('path');
const checkFrontmatter = require('../../src/checks/frontmatter');
const { BASE_CONFIG } = require('../../src/base-config');

const GOOD = `---
capability: feature-a
last-updated: 2026-01-15
---

# Feature A

## Overview
`;

const BAD_NO_CAPABILITY = `---
last-updated: 2026-01-15
---

# Feature A
`;

const BAD_WRONG_CAPABILITY = `---
capability: wrong-slug
last-updated: 2026-01-15
---

# Feature A
`;

const BAD_DATE = `---
capability: feature-a
last-updated: 15-01-2026
---

# Feature A
`;

const filePath = '/specs/feature-a.md';

describe('checkFrontmatter', () => {
  test('returns no violations for valid frontmatter', () => {
    expect(checkFrontmatter(filePath, GOOD, BASE_CONFIG)).toHaveLength(0);
  });

  test('fails when capability field is missing', () => {
    const v = checkFrontmatter(filePath, BAD_NO_CAPABILITY, BASE_CONFIG);
    expect(v.length).toBeGreaterThan(0);
    expect(v[0].rule).toBe('frontmatter');
    expect(v[0].detail).toMatch(/capability/);
  });

  test('fails when capability does not match file slug', () => {
    const v = checkFrontmatter(filePath, BAD_WRONG_CAPABILITY, BASE_CONFIG);
    expect(v.some(vio => vio.detail.match(/slug/))).toBe(true);
  });

  test('fails when last-updated is not YYYY-MM-DD', () => {
    const v = checkFrontmatter(filePath, BAD_DATE, BASE_CONFIG);
    expect(v.some(vio => vio.detail.match(/last-updated|YYYY/))).toBe(true);
  });

  test('fails when no frontmatter block', () => {
    const v = checkFrontmatter(filePath, '# No frontmatter', BASE_CONFIG);
    expect(v.length).toBeGreaterThan(0);
  });
});

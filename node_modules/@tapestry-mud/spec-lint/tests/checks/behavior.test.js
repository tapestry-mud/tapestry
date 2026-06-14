'use strict';

const checkBehavior = require('../../src/checks/behavior');
const { BASE_CONFIG } = require('../../src/base-config');

const WITH_ANCHOR = `---
capability: feature-a
last-updated: 2026-01-15
---

## Overview

.

## Behavior

- Routes on first token. (src/CommandRouter.cs:26-64)

## Rejected and Reverted

- None on record.

## Change Log
`;

const NO_ANCHOR = `---
capability: feature-a
last-updated: 2026-01-15
---

## Overview

.

## Behavior

- Routes on first token. No file reference here.

## Rejected and Reverted

- None on record.

## Change Log
`;

describe('checkBehavior', () => {
  test('passes when Behavior has an anchor', () => {
    expect(checkBehavior('feature-a.md', WITH_ANCHOR, BASE_CONFIG)).toHaveLength(0);
  });

  test('fails when Behavior has no anchor match', () => {
    const v = checkBehavior('feature-a.md', NO_ANCHOR, BASE_CONFIG);
    expect(v.length).toBeGreaterThan(0);
    expect(v[0].rule).toBe('behavior');
  });

  test('passes when anchor uses whole-file form (no line number)', () => {
    const content = WITH_ANCHOR.replace('(src/CommandRouter.cs:26-64)', '(src/CommandRouter.cs)');
    expect(checkBehavior('feature-a.md', content, BASE_CONFIG)).toHaveLength(0);
  });

  test('returns no violations when Behavior section is missing (sections check owns that)', () => {
    const noSection = `---
capability: feature-a
last-updated: 2026-01-15
---

## Overview

.
`;
    expect(checkBehavior('feature-a.md', noSection, BASE_CONFIG)).toHaveLength(0);
  });
});

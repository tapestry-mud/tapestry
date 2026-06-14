'use strict';

const { parseFrontmatter, parseSections } = require('../src/parser');

const SAMPLE = `---
capability: command-dispatch
last-updated: 2026-06-12
---

# Command Dispatch

## Overview

Routes commands.

## Behavior

- Dispatches. (src/CommandRouter.cs:26-64)

## Rejected and Reverted

- None on record.

## Change Log
`;

const CHANGE_RECORD = `---
release: v0.1.0
specs: [feature-a.md, feature-b.md]
status: reverted
---

# Change
`;

describe('parseFrontmatter', () => {
  test('parses scalar fields', () => {
    const fm = parseFrontmatter(SAMPLE);
    expect(fm['capability']).toBe('command-dispatch');
    expect(fm['last-updated']).toBe('2026-06-12');
  });

  test('parses list fields (inline array)', () => {
    const fm = parseFrontmatter(CHANGE_RECORD);
    expect(fm['specs']).toEqual(['feature-a.md', 'feature-b.md']);
    expect(fm['status']).toBe('reverted');
  });

  test('returns null when no frontmatter block', () => {
    expect(parseFrontmatter('# No frontmatter\n\nJust text.')).toBeNull();
  });
});

describe('parseSections', () => {
  test('returns map of section name -> body', () => {
    const sections = parseSections(SAMPLE);
    expect(sections['Overview']).toBe('Routes commands.');
    expect(sections['Behavior']).toContain('Dispatches');
    expect(sections['Rejected and Reverted']).toBe('- None on record.');
    expect(sections['Change Log']).toBe('');
  });

  test('strips frontmatter before parsing', () => {
    const sections = parseSections(SAMPLE);
    expect('---' in sections).toBe(false);
  });

  test('skips thematic breaks (--- horizontal rules) within section content', () => {
    const content = `---
capability: x
last-updated: 2026-01-01
---

# X

## Overview

Brief.

---

## Rejected and Reverted

- None on record.

---

## Change Log
`;
    const sections = parseSections(content);
    expect(sections['Rejected and Reverted']).toBe('- None on record.');
    expect(sections['Overview']).toBe('Brief.');
  });
});

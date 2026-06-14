'use strict';

const path = require('path');
const { checkIndexSync } = require('../../src/checks/index-sync');
const { BASE_CONFIG } = require('../../src/base-config');

const GOOD_README = `# Specs

| Capability | File | Last Updated |
|------------|------|--------------|
| Feature A | [feature-a.md](feature-a.md) | 2026-01-15 |
`;

const goodSpecs = [
  { path: '/specs/feature-a.md', frontmatter: { 'last-updated': '2026-01-15' } },
];

describe('checkIndexSync', () => {
  test('passes when index and disk match', () => {
    const v = checkIndexSync('/specs', goodSpecs, GOOD_README, BASE_CONFIG);
    expect(v).toHaveLength(0);
  });

  test('fails (direction A) when index names file not on disk', () => {
    const readme = `# Specs

| Capability | File | Last Updated |
|------------|------|--------------|
| Feature A | [feature-a.md](feature-a.md) | 2026-01-15 |
| Ghost | [ghost.md](ghost.md) | 2026-01-15 |
`;
    const v = checkIndexSync('/specs', goodSpecs, readme, BASE_CONFIG);
    expect(v.some(x => x.detail.includes('ghost.md') && x.detail.includes('not exist'))).toBe(true);
  });

  test('fails (direction B) when file on disk not in index', () => {
    const twoSpecs = [
      ...goodSpecs,
      { path: '/specs/feature-b.md', frontmatter: { 'last-updated': '2026-01-20' } },
    ];
    const v = checkIndexSync('/specs', twoSpecs, GOOD_README, BASE_CONFIG);
    expect(v.some(x => x.detail.includes('feature-b.md') && x.detail.includes('not in'))).toBe(true);
  });

  test('fails when index date does not match frontmatter last-updated', () => {
    const wrongDate = GOOD_README.replace('2026-01-15', '2026-02-01');
    const v = checkIndexSync('/specs', goodSpecs, wrongDate, BASE_CONFIG);
    expect(v.some(x => x.detail.includes('date') || x.detail.includes('last-updated'))).toBe(true);
  });
});

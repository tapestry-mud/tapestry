'use strict';

const path = require('path');
const { lint } = require('../src/engine');
const { BASE_CONFIG } = require('../src/base-config');

const FIXTURES = path.join(__dirname, 'fixtures');

describe('lint (integration)', () => {
  test('complete fixture has zero violations', () => {
    const specsDir = path.join(FIXTURES, 'good/complete/specs');
    const result = lint(specsDir, BASE_CONFIG);
    const allViolations = [
      ...Object.values(result.fileResults).flat(),
      ...result.crossFileViolations,
    ];
    expect(allViolations).toHaveLength(0);
  });

  test('seeded-empty fixture has zero violations', () => {
    const specsDir = path.join(FIXTURES, 'good/seeded-empty/specs');
    const result = lint(specsDir, BASE_CONFIG);
    const allViolations = [
      ...Object.values(result.fileResults).flat(),
      ...result.crossFileViolations,
    ];
    expect(allViolations).toHaveLength(0);
  });

  test('check1 bad fixture produces frontmatter violations', () => {
    const specsDir = path.join(FIXTURES, 'bad/check1-bad-frontmatter/specs');
    const result = lint(specsDir, BASE_CONFIG);
    const v = Object.values(result.fileResults).flat();
    expect(v.some(x => x.rule === 'frontmatter')).toBe(true);
  });

  test('check6a bad fixture produces index-sync violation (missing disk)', () => {
    const specsDir = path.join(FIXTURES, 'bad/check6a-index-missing-disk/specs');
    const result = lint(specsDir, BASE_CONFIG);
    expect(result.crossFileViolations.some(x => x.rule === 'index-sync')).toBe(true);
  });

  test('check7a bad fixture produces currency violation (empty changelog has record)', () => {
    const specsDir = path.join(FIXTURES, 'bad/check7a-empty-changelog-has-record/specs');
    const result = lint(specsDir, BASE_CONFIG);
    expect(result.crossFileViolations.some(x => x.rule === 'currency')).toBe(true);
  });

  test('check8 bad fixture produces tombstone violation', () => {
    const specsDir = path.join(FIXTURES, 'bad/check8-missing-tombstone/specs');
    const result = lint(specsDir, BASE_CONFIG);
    expect(result.crossFileViolations.some(x => x.rule === 'tombstone')).toBe(true);
  });

  test('check9 bad fixture produces readme-drift violation', () => {
    const specsDir = path.join(FIXTURES, 'bad/check9-readme-drift/specs');
    const result = lint(specsDir, BASE_CONFIG);
    expect(result.crossFileViolations.some(x => x.rule === 'readme-drift')).toBe(true);
  });
});

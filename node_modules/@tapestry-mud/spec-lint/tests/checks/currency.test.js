'use strict';

const path = require('path');
const { checkCurrency } = require('../../src/checks/currency');
const { BASE_CONFIG } = require('../../src/base-config');

function makeSpec(logContent, lastUpdated) {
  return {
    path: '/specs/feature-a.md',
    frontmatter: { 'last-updated': lastUpdated },
    sections: {
      'Change Log': logContent,
    },
  };
}

function makeRecord(date, slug, specsList) {
  return {
    date,
    slug,
    frontmatter: { specs: specsList },
  };
}

describe('checkCurrency', () => {
  test('passes when changelog refs latest record and date is current', () => {
    const spec = makeSpec('- 2026-01-15 [feature-a-v1](changes/2026-01-15-feature-a-v1.md)', '2026-01-15');
    const record = makeRecord('2026-01-15', 'feature-a-v1', ['feature-a.md']);
    expect(checkCurrency([spec], [record], BASE_CONFIG)).toHaveLength(0);
  });

  test('passes (seeded-empty) when no records name the capability', () => {
    const spec = makeSpec('', '2026-01-01');
    expect(checkCurrency([spec], [], BASE_CONFIG)).toHaveLength(0);
  });

  test('fails (7a) when record names cap but changelog is empty', () => {
    const spec = makeSpec('', '2026-01-15');
    const record = makeRecord('2026-01-15', 'feature-a-v1', ['feature-a.md']);
    const v = checkCurrency([spec], [record], BASE_CONFIG);
    expect(v.length).toBeGreaterThan(0);
    expect(v[0].rule).toBe('currency');
  });

  test('fails (7b) when changelog refs old record but last-updated is too old', () => {
    const spec = makeSpec('- 2026-01-15 [feature-a-v1](changes/2026-01-15-feature-a-v1.md)', '2026-01-01');
    const record = makeRecord('2026-01-15', 'feature-a-v1', ['feature-a.md']);
    const v = checkCurrency([spec], [record], BASE_CONFIG);
    expect(v.some(x => x.detail.includes('last-updated'))).toBe(true);
  });

  test('fails when top changelog entry does not reference latest record', () => {
    const spec = makeSpec('- 2026-01-01 [feature-a-v0](changes/2026-01-01-feature-a-v0.md)', '2026-01-15');
    const record = makeRecord('2026-01-15', 'feature-a-v1', ['feature-a.md']);
    const v = checkCurrency([spec], [record], BASE_CONFIG);
    expect(v.some(x => x.detail.includes('feature-a-v1'))).toBe(true);
  });

  test('passes when two same-date records name the same capability and either is the top entry', () => {
    const specA = makeSpec('- 2026-01-15 [alpha-fix](changes/2026-01-15-alpha-fix.md)\n- 2026-01-15 [beta-fix](changes/2026-01-15-beta-fix.md)', '2026-01-15');
    const specB = makeSpec('- 2026-01-15 [beta-fix](changes/2026-01-15-beta-fix.md)\n- 2026-01-15 [alpha-fix](changes/2026-01-15-alpha-fix.md)', '2026-01-15');
    const records = [
      makeRecord('2026-01-15', 'alpha-fix', ['feature-a.md']),
      makeRecord('2026-01-15', 'beta-fix', ['feature-a.md']),
    ];
    expect(checkCurrency([specA], records, BASE_CONFIG)).toHaveLength(0);
    expect(checkCurrency([specB], records, BASE_CONFIG)).toHaveLength(0);
  });
});

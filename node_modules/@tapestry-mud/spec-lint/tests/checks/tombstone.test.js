'use strict';

const path = require('path');
const { checkTombstone } = require('../../src/checks/tombstone');
const { BASE_CONFIG } = require('../../src/base-config');

function makeSpec(rrContent) {
  return {
    path: '/specs/feature-a.md',
    sections: { 'Rejected and Reverted': rrContent },
  };
}

function makeRecord(slug, specsList, status) {
  return { slug, frontmatter: { specs: specsList, status } };
}

describe('checkTombstone', () => {
  test('passes when reverted record has tombstone entry', () => {
    const spec = makeSpec('- Rejected idea X because Y.\n- 2026-01-01 reverted behavior Z.');
    const record = makeRecord('some-change', ['feature-a.md'], 'reverted');
    expect(checkTombstone([spec], [record], BASE_CONFIG)).toHaveLength(0);
  });

  test('fails when reverted record has no tombstone (only sentinel)', () => {
    const spec = makeSpec('- None on record.');
    const record = makeRecord('some-change', ['feature-a.md'], 'reverted');
    const v = checkTombstone([spec], [record], BASE_CONFIG);
    expect(v.length).toBeGreaterThan(0);
    expect(v[0].rule).toBe('tombstone');
  });

  test('fails when reverted record has empty R&R', () => {
    const spec = makeSpec('');
    const record = makeRecord('some-change', ['feature-a.md'], 'reverted');
    const v = checkTombstone([spec], [record], BASE_CONFIG);
    expect(v.length).toBeGreaterThan(0);
  });

  test('passes when record has no status:reverted', () => {
    const spec = makeSpec('- None on record.');
    const record = makeRecord('some-change', ['feature-a.md'], undefined);
    expect(checkTombstone([spec], [record], BASE_CONFIG)).toHaveLength(0);
  });
});

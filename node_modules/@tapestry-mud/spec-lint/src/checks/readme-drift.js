'use strict';

const { extractManagedBlock, renderContract } = require('../renderer');

function checkReadmeDrift(readmeContent, effectiveConfig) {
  const extracted = extractManagedBlock(readmeContent);
  if (extracted === null) {
    return []; // absent block is not a violation (init handles seeding)
  }
  const expected = renderContract(effectiveConfig);
  if (extracted.trim() === expected.trim()) {
    return [];
  }
  return [{
    rule: 'readme-drift',
    detail: 'README managed block does not match the rendered effective ruleset - run spec-lint --explain to see the expected content',
  }];
}

module.exports = { checkReadmeDrift };

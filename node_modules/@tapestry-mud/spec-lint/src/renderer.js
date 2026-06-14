'use strict';

const { BASE_CONFIG } = require('./base-config');

const MANAGED_BLOCK_START = '<!-- spec-lint:start -->';
const MANAGED_BLOCK_END = '<!-- spec-lint:end -->';

function renderContract(effectiveConfig) {
  const sections = effectiveConfig.sections || BASE_CONFIG.sections;
  const anchorRegex = effectiveConfig.anchorRegex || BASE_CONFIG.anchorRegex;
  const sentinelText = effectiveConfig.sentinelText || BASE_CONFIG.sentinelText;
  const mode = effectiveConfig.mode || BASE_CONFIG.defaultMode;

  return [
    `Mode: ${mode}`,
    '',
    `Required sections: ${sections.join(', ')}`,
    '',
    `Anchor regex (Behavior): ${anchorRegex}`,
    '',
    `Empty-reversal sentinel: ${sentinelText}`,
    '',
    'Change Log: list, newest-first by date, not a table. Empty is valid for unmodified capabilities.',
    '',
    'Index sync: every capability .md on disk appears in README index; every indexed file exists on disk; index date matches file last-updated.',
    '',
    'Currency: for each change record naming a capability, the top Change Log entry references that record and last-updated >= record date. A capability named by zero records may have an empty Change Log.',
    '',
    'Tombstone: a change record with status:reverted requires a tombstone entry in the capability Rejected and Reverted (not the empty sentinel).',
  ].join('\n');
}

function renderManagedBlock(effectiveConfig) {
  return `${MANAGED_BLOCK_START}\n${renderContract(effectiveConfig)}\n${MANAGED_BLOCK_END}`;
}

function extractManagedBlock(readmeContent) {
  const startIdx = readmeContent.indexOf(MANAGED_BLOCK_START);
  const endIdx = readmeContent.indexOf(MANAGED_BLOCK_END);
  if (startIdx === -1 || endIdx === -1) { return null; }
  return readmeContent.slice(startIdx + MANAGED_BLOCK_START.length, endIdx).trim();
}

module.exports = { renderContract, renderManagedBlock, extractManagedBlock, MANAGED_BLOCK_START, MANAGED_BLOCK_END };

'use strict';

const fs = require('fs');
const path = require('path');
const { BASE_CONFIG } = require('./base-config');

function loadConfig(specsDir) {
  const configPath = path.join(specsDir, 'lint.config.json');
  let repoConfig = {};
  if (fs.existsSync(configPath)) {
    repoConfig = JSON.parse(fs.readFileSync(configPath, 'utf8'));
  }

  const sections = buildSections(repoConfig.sections);
  const mode = repoConfig.mode || BASE_CONFIG.defaultMode;

  return {
    sections,
    anchorRegex: repoConfig.anchorRegex || BASE_CONFIG.anchorRegex,
    sentinelText: repoConfig.sentinelText || BASE_CONFIG.sentinelText,
    mode,
    rules: Object.assign({}, BASE_CONFIG.rules, repoConfig.rules),
  };
}

function buildSections(repoSections) {
  if (!repoSections) { return BASE_CONFIG.sections; }
  if (Array.isArray(repoSections)) { return repoSections; }
  const extra = repoSections.extra || [];
  return [...BASE_CONFIG.sections, ...extra];
}

module.exports = { loadConfig };

'use strict';

const { parseSections } = require('../parser');

function checkSections(filePath, content, config) {
  const violations = [];
  const sections = parseSections(content);
  const required = config.sections;
  for (const name of required) {
    if (!(name in sections)) {
      violations.push({ rule: 'sections', detail: `missing required section: ## ${name}` });
    }
  }
  return violations;
}

module.exports = checkSections;

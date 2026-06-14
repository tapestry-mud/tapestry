'use strict';

const path = require('path');
const { parseFrontmatter } = require('../parser');

function checkFrontmatter(filePath, content, config) {
  const violations = [];
  const fm = parseFrontmatter(content);
  if (!fm) {
    violations.push({ rule: 'frontmatter', detail: 'no frontmatter block found' });
    return violations;
  }
  const slug = path.basename(filePath, '.md');
  if (!fm['capability']) {
    violations.push({ rule: 'frontmatter', detail: 'missing required field: capability' });
  } else if (fm['capability'] !== slug) {
    violations.push({ rule: 'frontmatter', detail: `capability "${fm['capability']}" does not match file slug "${slug}"` });
  }
  if (!fm['last-updated']) {
    violations.push({ rule: 'frontmatter', detail: 'missing required field: last-updated' });
  } else if (!/^\d{4}-\d{2}-\d{2}$/.test(fm['last-updated'])) {
    violations.push({ rule: 'frontmatter', detail: `last-updated "${fm['last-updated']}" is not a valid YYYY-MM-DD date` });
  }
  return violations;
}

module.exports = checkFrontmatter;

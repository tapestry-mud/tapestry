'use strict';

const fs = require('fs');
const path = require('path');
const { parseFrontmatter, parseSections } = require('./parser');
const checkFrontmatter = require('./checks/frontmatter');
const checkSections = require('./checks/sections');
const checkBehavior = require('./checks/behavior');
const checkSentinel = require('./checks/sentinel');
const checkChangelog = require('./checks/changelog');
const { checkIndexSync } = require('./checks/index-sync');
const { checkCurrency } = require('./checks/currency');
const { checkTombstone } = require('./checks/tombstone');
const { checkReadmeDrift } = require('./checks/readme-drift');

function lint(specsDir, effectiveConfig) {
  const allFiles = fs.readdirSync(specsDir);
  const specFiles = allFiles
    .filter(f => f.endsWith('.md') && f !== 'README.md')
    .map(f => path.join(specsDir, f));

  const specs = specFiles.map(filePath => {
    const content = fs.readFileSync(filePath, 'utf8');
    return {
      path: filePath,
      content,
      frontmatter: parseFrontmatter(content),
      sections: parseSections(content),
    };
  });

  const readmePath = path.join(specsDir, 'README.md');
  const readmeContent = fs.existsSync(readmePath)
    ? fs.readFileSync(readmePath, 'utf8')
    : '';

  const changesDir = path.join(specsDir, 'changes');
  const changeRecords = [];
  if (fs.existsSync(changesDir)) {
    const changeFiles = fs.readdirSync(changesDir).filter(f => f.endsWith('.md'));
    for (const cf of changeFiles) {
      const cfPath = path.join(changesDir, cf);
      const cfContent = fs.readFileSync(cfPath, 'utf8');
      const dateSlug = cf.replace('.md', '');
      const dateMatch = dateSlug.match(/^(\d{4}-\d{2}-\d{2})-(.+)$/);
      changeRecords.push({
        path: cfPath,
        filename: cf,
        date: dateMatch ? dateMatch[1] : null,
        slug: dateMatch ? dateMatch[2] : dateSlug,
        content: cfContent,
        frontmatter: parseFrontmatter(cfContent),
      });
    }
  }

  const fileResults = {};
  for (const spec of specs) {
    fileResults[spec.path] = [
      ...checkFrontmatter(spec.path, spec.content, effectiveConfig),
      ...checkSections(spec.path, spec.content, effectiveConfig),
      ...checkBehavior(spec.path, spec.content, effectiveConfig),
      ...checkSentinel(spec.path, spec.content, effectiveConfig),
      ...checkChangelog(spec.path, spec.content, effectiveConfig),
    ];
  }

  const crossFileViolations = [
    ...checkIndexSync(specsDir, specs, readmeContent, effectiveConfig),
    ...checkCurrency(specs, changeRecords, effectiveConfig),
    ...checkTombstone(specs, changeRecords, effectiveConfig),
    ...checkReadmeDrift(readmeContent, effectiveConfig),
  ];

  return { specs, fileResults, crossFileViolations };
}

module.exports = { lint };

'use strict';

const path = require('path');

function formatResults(specsDir, lintResult, mode) {
  const { fileResults, crossFileViolations } = lintResult;
  const lines = [];
  let violationCount = 0;
  const label = mode === 'strict' ? 'FAIL' : 'WARN';

  for (const [filePath, violations] of Object.entries(fileResults)) {
    const rel = path.relative(specsDir, filePath);
    if (violations.length === 0) {
      lines.push(`OK  ${rel}`);
    } else {
      for (const v of violations) {
        lines.push(`${label}  ${rel}  ${v.rule}: ${v.detail}`);
        violationCount++;
      }
    }
  }

  for (const v of crossFileViolations) {
    const fileLabel = v.file ? path.relative(specsDir, v.file) + '  ' : '';
    lines.push(`${label}  ${fileLabel}${v.rule}: ${v.detail}`);
    violationCount++;
  }

  const fileCount = Object.keys(fileResults).length;
  if (violationCount === 0) {
    lines.push(`\n${fileCount} files, 0 violations`);
  } else if (mode === 'strict') {
    lines.push(`\n${fileCount} files, ${violationCount} failures`);
  } else {
    lines.push(`\n${fileCount} files, ${violationCount} warnings (lenient: not blocking)`);
  }

  return lines.join('\n');
}

module.exports = { formatResults };

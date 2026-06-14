'use strict';

const path = require('path');

function checkTombstone(specs, changeRecords, config) {
  const violations = [];
  const sentinel = config.sentinelText;
  const revertedRecords = changeRecords.filter(r => r.frontmatter && r.frontmatter.status === 'reverted');

  for (const record of revertedRecords) {
    const fm = record.frontmatter;
    if (!fm.specs) { continue; }
    const specsList = Array.isArray(fm.specs) ? fm.specs : [fm.specs];

    for (const capFile of specsList) {
      const spec = specs.find(s => path.basename(s.path) === capFile);
      if (!spec) { continue; }

      const rrText = (spec.sections && spec.sections['Rejected and Reverted']) || '';
      const lines = rrText.split('\n').map(l => l.trim()).filter(l => l.startsWith('- ') && l !== sentinel);
      if (lines.length === 0) {
        violations.push({
          rule: 'tombstone',
          file: spec.path,
          detail: `${capFile}: change record ${record.slug} has status:reverted but ${capFile} has no tombstone entry in Rejected and Reverted`,
        });
      }
    }
  }

  return violations;
}

module.exports = { checkTombstone };

'use strict';

const { parseSections } = require('../parser');

function checkChangelog(filePath, content, config) {
  const sections = parseSections(content);
  const clText = sections['Change Log'];
  if (clText === undefined) {
    return [];
  }
  if (clText.trim() === '') {
    return []; // seeded-empty case, valid
  }
  const lines = clText.split('\n').map(l => l.trim()).filter(Boolean);
  if (lines.some(l => l.startsWith('|'))) {
    return [{ rule: 'changelog', detail: 'Change Log must be a list, not a table' }];
  }
  const dates = [];
  for (const line of lines) {
    const m = line.match(/^- (\d{4}-\d{2}-\d{2})\b/);
    if (m) {
      dates.push(m[1]);
    }
  }
  for (let i = 0; i < dates.length - 1; i++) {
    if (dates[i] < dates[i + 1]) {
      return [{
        rule: 'changelog',
        detail: `Change Log entries are not newest-first (${dates[i]} before ${dates[i + 1]})`,
      }];
    }
  }
  return [];
}

module.exports = checkChangelog;

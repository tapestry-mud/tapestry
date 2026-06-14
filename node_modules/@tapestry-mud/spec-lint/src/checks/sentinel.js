'use strict';

const { parseSections } = require('../parser');

function checkSentinel(filePath, content, config) {
  const sections = parseSections(content);
  const rrText = sections['Rejected and Reverted'];
  if (rrText === undefined) {
    return [];
  }
  const sentinel = config.sentinelText;
  const lines = rrText.split('\n').map(l => l.trim()).filter(Boolean);
  const hasRealRecord = lines.some(l => l.startsWith('- ') && l !== sentinel);
  if (hasRealRecord) {
    return []; // real entries present, sentinel not required
  }
  if (rrText.trim() !== sentinel) {
    return [{
      rule: 'sentinel',
      detail: `empty Rejected and Reverted must contain exactly: ${sentinel}`,
    }];
  }
  return [];
}

module.exports = checkSentinel;

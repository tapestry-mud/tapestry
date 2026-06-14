'use strict';

const { parseSections } = require('../parser');

function checkBehavior(filePath, content, config) {
  const sections = parseSections(content);
  const behaviorText = sections['Behavior'];
  if (behaviorText === undefined) {
    return []; // check 2 owns the missing-section violation
  }
  const re = new RegExp(config.anchorRegex);
  if (!re.test(behaviorText)) {
    return [{
      rule: 'behavior',
      detail: 'Behavior section contains no inline anchors matching the pinned regex',
    }];
  }
  return [];
}

module.exports = checkBehavior;

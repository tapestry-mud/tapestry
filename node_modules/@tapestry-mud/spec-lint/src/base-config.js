'use strict';

const BASE_CONFIG = {
  sections: [
    'Overview',
    'Behavior',
    'Rejected and Reverted',
    'Change Log',
  ],
  anchorRegex: String.raw`\([@\w./\\-]+\.(cs|js|ts|json|ya?ml)(:\d+(-\d+)?)?[^)]*\)`,
  sentinelText: '- None on record.',
  defaultMode: 'strict',
};

module.exports = { BASE_CONFIG };

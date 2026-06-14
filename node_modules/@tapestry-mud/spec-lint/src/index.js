'use strict';

const { lint } = require('./engine');
const { loadConfig } = require('./config');
const { formatResults } = require('./formatter');
const { renderContract, renderManagedBlock, extractManagedBlock } = require('./renderer');
const { BASE_CONFIG } = require('./base-config');

module.exports = {
  lint,
  loadConfig,
  formatResults,
  renderContract,
  renderManagedBlock,
  extractManagedBlock,
  BASE_CONFIG,
};

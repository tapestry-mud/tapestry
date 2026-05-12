'use strict';

const { startEngine } = require('../lib/engine-manager');

async function startCmd({ cwd = process.cwd() } = {}) {
  await startEngine(cwd);
}

module.exports = { startCmd };

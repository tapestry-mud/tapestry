'use strict';

const { stopEngine } = require('../lib/engine-manager');

async function stopCmd({ cwd = process.cwd() } = {}) {
  await stopEngine(cwd);
}

module.exports = { stopCmd };

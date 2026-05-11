'use strict';

const fs = require('fs');
const path = require('path');
const { enablePackage } = require('../lib/boot');

async function enable(packageName, { cwd = process.cwd() } = {}) {
  const manifestPath = path.join(cwd, 'tapestry.yaml');
  if (!fs.existsSync(manifestPath)) {
    throw new Error('No tapestry.yaml found. Run `tapestry init` first.');
  }
  enablePackage(cwd, packageName);
  console.log(`Enabled ${packageName}.`);
}

module.exports = { enable };

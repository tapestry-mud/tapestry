'use strict';

const fs = require('fs');
const path = require('path');
const { disablePackage } = require('../lib/boot');

async function disable(packageName, { cwd = process.cwd() } = {}) {
  const manifestPath = path.join(cwd, 'tapestry.yaml');
  if (!fs.existsSync(manifestPath)) {
    throw new Error('No tapestry.yaml found. Run `tapestry init` first.');
  }
  disablePackage(cwd, packageName);
  console.log(`Disabled ${packageName}. Files remain on disk. Run \`tapestry enable ${packageName}\` to re-activate.`);
}

module.exports = { disable };

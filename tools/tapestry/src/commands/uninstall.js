'use strict';

const fs = require('fs');
const path = require('path');
const { readYaml, writeYaml } = require('../util/yaml');
const { readLock, writeLock } = require('../lib/lock-file');
const { removePackageFromBoot } = require('../lib/boot');

function packInstallPath(cwd, packageName) {
  const parts = packageName.split('/');
  return path.join(cwd, 'packs', ...parts);
}

async function uninstall(packageName, { cwd = process.cwd() } = {}) {
  const manifestPath = path.join(cwd, 'tapestry.yaml');
  if (!fs.existsSync(manifestPath)) {
    throw new Error('No tapestry.yaml found. Run `tapestry init` first.');
  }

  const manifest = readYaml(manifestPath);
  if (!manifest.dependencies || !manifest.dependencies[packageName]) {
    throw new Error(`${packageName} is not in tapestry.yaml`);
  }

  const destDir = packInstallPath(cwd, packageName);
  if (fs.existsSync(destDir)) {
    fs.rmSync(destDir, { recursive: true });
    console.log(`  removed ${packageName}`);
  }

  delete manifest.dependencies[packageName];
  writeYaml(manifestPath, manifest);

  const lock = readLock(cwd);
  if (lock && lock.resolved) {
    delete lock.resolved[packageName];
    writeLock(cwd, lock);
  }

  removePackageFromBoot(cwd, packageName);

  console.log(`Uninstalled ${packageName}.`);
  console.log('Note: transitive dependencies are not automatically removed. Run `tapestry install` to refresh.');
}

module.exports = { uninstall };

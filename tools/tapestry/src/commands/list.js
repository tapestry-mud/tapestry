'use strict';

const fs = require('fs');
const path = require('path');
const { readLock } = require('../lib/lock-file');
const { readBoot } = require('../lib/boot');
const { readYaml } = require('../util/yaml');

function packInstallPath(cwd, packageName) {
  const parts = packageName.split('/');
  return path.join(cwd, 'packs', ...parts);
}

async function list({ cwd = process.cwd() } = {}) {
  const lock = readLock(cwd);
  if (!lock || !lock.resolved || !Object.keys(lock.resolved).length) {
    console.log('No packages installed.');
    return;
  }

  const boot = readBoot(cwd);
  const packages = Object.entries(lock.resolved);

  const nameWidth = Math.max(7, ...packages.map(([n]) => n.length));
  const verWidth = Math.max(7, ...packages.map(([, r]) => r.version.length));

  console.log(
    `${'PACKAGE'.padEnd(nameWidth)}  ${'VERSION'.padEnd(verWidth)}  TYPE      STATUS`
  );

  for (const [pkgName, resolved] of packages) {
    const enabled = boot.packs[pkgName]?.enabled !== false ? 'enabled' : 'disabled';

    let type = '';
    const packManifestPath = path.join(packInstallPath(cwd, pkgName), 'tapestry.yaml');
    if (fs.existsSync(packManifestPath)) {
      try {
        type = readYaml(packManifestPath).type || '';
      } catch {
        //
      }
    }

    console.log(
      `${pkgName.padEnd(nameWidth)}  ${resolved.version.padEnd(verWidth)}  ${type.padEnd(8)}  ${enabled}`
    );
  }
}

module.exports = { list };

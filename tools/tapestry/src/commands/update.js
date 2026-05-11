'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { readYaml } = require('../util/yaml');
const { resolve } = require('../lib/semver-resolver');
const { readLock, writeLock } = require('../lib/lock-file');
const { fetchTarball, DEFAULT_REGISTRY } = require('../lib/registry-client');
const { verifyIntegrity, saveTarball, extractTarball } = require('../lib/tarball');
const { addPackageToBoot } = require('../lib/boot');

function packInstallPath(cwd, packageName) {
  const parts = packageName.split('/');
  return path.join(cwd, 'packs', ...parts);
}

async function update(packageArg, { cwd = process.cwd(), registryUrl = DEFAULT_REGISTRY } = {}) {
  const manifestPath = path.join(cwd, 'tapestry.yaml');
  if (!fs.existsSync(manifestPath)) {
    throw new Error('No tapestry.yaml found. Run `tapestry init` first.');
  }

  const manifest = readYaml(manifestPath);
  const allDeps = manifest.dependencies || {};

  let depsToResolve;
  if (packageArg) {
    if (!allDeps[packageArg]) {
      throw new Error(`${packageArg} is not in tapestry.yaml`);
    }
    depsToResolve = { [packageArg]: allDeps[packageArg] };
  } else {
    depsToResolve = allDeps;
  }

  console.log('Resolving latest compatible versions...');
  const freshResolved = await resolve(depsToResolve, registryUrl);

  const existingLock = readLock(cwd);
  const existingResolved = (existingLock && existingLock.resolved) || {};
  const mergedResolved = Object.assign({}, existingResolved, freshResolved);

  for (const [packageName, info] of Object.entries(freshResolved)) {
    const currentVersion = existingResolved[packageName] && existingResolved[packageName].version;
    if (currentVersion === info.version) {
      console.log(`  up to date ${packageName}@${info.version}`);
      continue;
    }

    console.log(`  updating ${packageName} ${currentVersion || 'not installed'} -> ${info.version}`);

    const destDir = packInstallPath(cwd, packageName);
    if (fs.existsSync(destDir)) {
      fs.rmSync(destDir, { recursive: true });
    }

    const safeId = packageName.replace('@', '').replace('/', '-');
    const tmpPath = path.join(os.tmpdir(), `tapestry-${safeId}-${info.version}.tgz`);

    try {
      const buffer = await fetchTarball(info.tarball);
      verifyIntegrity(buffer, info.integrity);
      saveTarball(buffer, tmpPath);
      await extractTarball(tmpPath, destDir);
    } finally {
      if (fs.existsSync(tmpPath)) {
        fs.unlinkSync(tmpPath);
      }
    }

    const packManifest = readYaml(path.join(destDir, 'tapestry.yaml'));
    addPackageToBoot(cwd, packageName, packManifest);
  }

  writeLock(cwd, { lockfile_version: 1, resolved: mergedResolved });
  console.log('Done.');
}

module.exports = { update };

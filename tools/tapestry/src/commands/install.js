'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { readYaml, writeYaml } = require('../util/yaml');
const { resolve } = require('../lib/semver-resolver');
const { readLock, writeLock } = require('../lib/lock-file');
const { fetchTarball, DEFAULT_REGISTRY } = require('../lib/registry-client');
const { verifyIntegrity, saveTarball, extractTarball } = require('../lib/tarball');
const { addPackageToBoot } = require('../lib/boot');

function packInstallPath(cwd, packageName) {
  const parts = packageName.split('/');
  return path.join(cwd, 'packs', ...parts);
}

function parsePackageArg(arg) {
  const match = arg.match(/^(@[^@/]+\/[^@]+)(?:@(.+))?$/);
  if (!match) {
    throw new Error(`Invalid package name: ${arg}. Expected @scope/name or @scope/name@range`);
  }
  return { name: match[1], rawRange: match[2] || null };
}

function isLockCurrent(manifestDeps, lock) {
  const lockResolved = lock.resolved || {};
  return Object.keys(manifestDeps).every((name) => lockResolved[name]);
}

async function installResolved(cwd, resolved) {
  for (const [packageName, info] of Object.entries(resolved)) {
    const destDir = packInstallPath(cwd, packageName);

    if (fs.existsSync(destDir)) {
      console.log(`  already installed ${packageName}@${info.version}`);
      continue;
    }

    console.log(`  installing ${packageName}@${info.version}`);

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
}

async function install(packageArg, { cwd = process.cwd(), registryUrl = DEFAULT_REGISTRY } = {}) {
  const manifestPath = path.join(cwd, 'tapestry.yaml');
  if (!fs.existsSync(manifestPath)) {
    throw new Error('No tapestry.yaml found. Run `tapestry init` first.');
  }

  const manifest = readYaml(manifestPath);
  let resolved;

  if (packageArg) {
    const { name, rawRange } = parsePackageArg(packageArg);
    manifest.dependencies = manifest.dependencies || {};
    const tempRange = rawRange || '*';
    manifest.dependencies[name] = tempRange;

    console.log('Resolving dependencies...');
    resolved = await resolve(manifest.dependencies, registryUrl);

    if (!rawRange) {
      manifest.dependencies[name] = `^${resolved[name].version}`;
    }

    writeYaml(manifestPath, manifest);
  } else {
    const lock = readLock(cwd);
    if (lock && isLockCurrent(manifest.dependencies || {}, lock)) {
      console.log('Installing from lock file...');
      resolved = lock.resolved;
    } else {
      console.log('Resolving dependencies...');
      resolved = await resolve(manifest.dependencies || {}, registryUrl);
    }
  }

  await installResolved(cwd, resolved);
  writeLock(cwd, { lockfile_version: 1, resolved });
  console.log('Done.');
}

module.exports = { install };

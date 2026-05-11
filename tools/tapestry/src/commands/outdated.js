'use strict';

const semver = require('semver');
const { readLock } = require('../lib/lock-file');
const { fetchPackageMetadata, DEFAULT_REGISTRY } = require('../lib/registry-client');

async function outdated({ cwd = process.cwd(), registryUrl = DEFAULT_REGISTRY } = {}) {
  const lock = readLock(cwd);
  if (!lock || !lock.resolved || !Object.keys(lock.resolved).length) {
    console.log('No packages installed.');
    return;
  }

  const packages = Object.entries(lock.resolved);
  const stale = [];

  for (const [pkgName, resolved] of packages) {
    let latestVersion;
    try {
      const data = await fetchPackageMetadata(pkgName, registryUrl);
      latestVersion = data.versions?.[0]?.version;
    } catch {
      continue;
    }
    if (latestVersion && semver.gt(latestVersion, resolved.version)) {
      stale.push({ name: pkgName, current: resolved.version, latest: latestVersion });
    }
  }

  if (!stale.length) {
    console.log('All packages are up to date.');
    return;
  }

  const nameWidth = Math.max(7, ...stale.map((s) => s.name.length));
  const curWidth = Math.max(7, ...stale.map((s) => s.current.length));
  console.log(`${'PACKAGE'.padEnd(nameWidth)}  ${'CURRENT'.padEnd(curWidth)}  LATEST`);
  for (const s of stale) {
    console.log(`${s.name.padEnd(nameWidth)}  ${s.current.padEnd(curWidth)}  ${s.latest}`);
  }
}

module.exports = { outdated };

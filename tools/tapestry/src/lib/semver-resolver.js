'use strict';

const semver = require('semver');
const { fetchPackageMetadata } = require('./registry-client');

async function resolve(dependencies, registryUrl) {
  if (!dependencies || Object.keys(dependencies).length === 0) {
    return {};
  }

  const baseUrl = registryUrl.replace(/\/$/, '');

  const resolved = {};
  const resolvedBy = {};
  const queue = Object.entries(dependencies).map(([name, range]) => ({
    name,
    range,
    requiredBy: 'root',
  }));

  while (queue.length > 0) {
    const { name, range, requiredBy } = queue.shift();

    if (resolved[name]) {
      if (!semver.satisfies(resolved[name].version, range)) {
        throw new Error(
          `CONFLICT: ${resolvedBy[name].requiredBy} requires ${name}@${resolvedBy[name].range}\n` +
          `          ${requiredBy} requires ${name}@${range}\n` +
          `          No version satisfies both ranges.`
        );
      }
      continue;
    }

    resolvedBy[name] = { range, requiredBy };

    const meta = await fetchPackageMetadata(name, baseUrl);
    const versions = meta.versions.map((v) => v.version);
    const best = semver.maxSatisfying(versions, range);

    if (!best) {
      throw new Error(
        `No version of ${name} satisfies ${range}. Available: ${versions.join(', ') || 'none'}`
      );
    }

    const versionData = meta.versions.find((v) => v.version === best);
    const manifest =
      typeof versionData.manifest === 'string'
        ? JSON.parse(versionData.manifest)
        : versionData.manifest;

    resolved[name] = {
      version: best,
      integrity: versionData.integrity,
      tarball: `${baseUrl}/v1/packages/${name}/${best}.tgz`,
    };

    const transDeps = manifest.dependencies || {};
    for (const [depName, depRange] of Object.entries(transDeps)) {
      queue.push({ name: depName, range: depRange, requiredBy: `${name}@${best}` });
    }

    const peerDeps = manifest.peerDependencies || {};
    for (const [peerName, peerRange] of Object.entries(peerDeps)) {
      if (!resolved[peerName] && !dependencies[peerName]) {
        console.warn(`  warn: optional peer ${peerName}@${peerRange} not installed`);
      }
    }
  }

  return resolved;
}

module.exports = { resolve };

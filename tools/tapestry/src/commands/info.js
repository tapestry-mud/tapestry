'use strict';

const { fetchPackageMetadata } = require('../lib/registry-client');

async function info(packageName, { registryUrl } = {}) {
  if (!packageName) {
    throw new Error('Usage: tapestry info <package>');
  }

  const data = await fetchPackageMetadata(packageName, registryUrl);
  const latest = data.versions?.[0];
  const m = latest?.manifest || {};

  console.log(data.name);
  if (m.description) {
    console.log(m.description);
  }
  console.log('');
  if (m.author) {
    console.log(`  Author:   ${m.author.handle}`);
  }
  if (m.license) {
    console.log(`  License:  ${m.license}`);
  }
  if (m.type) {
    console.log(`  Type:     ${m.type}`);
  }
  if (latest?.version) {
    console.log(`  Latest:   ${latest.version}`);
  }

  if (data.versions?.length > 1) {
    console.log(`  Versions: ${data.versions.map((v) => v.version).join('  ')}`);
  }

  const deps = m.dependencies ? Object.entries(m.dependencies) : [];
  if (deps.length) {
    console.log('');
    console.log('  Dependencies:');
    for (const [dep, range] of deps) {
      console.log(`    ${dep}  ${range}`);
    }
  }

  if (m.meta?.keywords?.length) {
    console.log('');
    console.log(`  Keywords: ${m.meta.keywords.join(', ')}`);
  }
}

module.exports = { info };

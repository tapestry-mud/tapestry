'use strict';

const fetch = require('node-fetch');

const DEFAULT_REGISTRY = process.env.TAPESTRY_REGISTRY || 'https://registry.tapestryengine.com';

async function fetchPackageMetadata(name, registryUrl = DEFAULT_REGISTRY) {
  const url = `${registryUrl}/v1/packages/${name}`;
  const res = await fetch(url);
  if (!res.ok) {
    if (res.status === 404) {
      throw new Error(`Package ${name} not found in registry`);
    }
    const body = await res.text();
    throw new Error(`Registry error ${res.status}: ${body}`);
  }
  return res.json();
}

async function fetchTarball(url) {
  const res = await fetch(url);
  if (!res.ok) {
    const body = await res.text();
    throw new Error(`Tarball download failed: ${res.status}: ${body}`);
  }
  return res.buffer();
}

module.exports = { fetchPackageMetadata, fetchTarball, DEFAULT_REGISTRY };

'use strict';

const fetch = require('node-fetch');

const DEFAULT_REGISTRY = process.env.TAPESTRY_REGISTRY || 'https://registry.tapestryengine.com';

function validatePackageName(name) {
  if (!name || typeof name !== 'string') {
    throw new Error('Package name must be a non-empty string');
  }
  if (!/^@[a-z0-9-]+\/[a-z0-9-]+/.test(name)) {
    throw new Error(`Invalid package name: ${name}. Expected @scope/name format`);
  }
  if (name.includes('..') || name.includes('//')) {
    throw new Error(`Invalid package name: ${name}. Path traversal not allowed`);
  }
}

async function fetchPackageMetadata(name, registryUrl = DEFAULT_REGISTRY) {
  validatePackageName(name);
  const url = `${registryUrl}/v1/packages/${name}`;
  const res = await fetch(url);
  if (!res.ok) {
    if (res.status === 404) {
      throw new Error(`Package ${name} not found in registry`);
    }
    const body = await res.text();
    throw new Error(`Registry error ${res.status}: ${body}`);
  }
  try {
    return await res.json();
  } catch (e) {
    throw new Error(`Invalid JSON response from registry for ${name}`);
  }
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

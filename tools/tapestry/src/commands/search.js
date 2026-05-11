'use strict';

const fetch = require('node-fetch');
const { DEFAULT_REGISTRY } = require('../lib/registry-client');

async function search(query, { registryUrl = DEFAULT_REGISTRY } = {}) {
  if (!query || !query.trim()) {
    throw new Error('Usage: tapestry search <query>');
  }

  const res = await fetch(
    `${registryUrl}/v1/search?q=${encodeURIComponent(query.trim())}`,
    undefined
  );

  if (!res.ok) {
    const body = await res.text();
    throw new Error(`Search failed (${res.status}): ${body}`);
  }

  const { results } = await res.json();

  if (!results.length) {
    console.log('No results found.');
    return;
  }

  const nameWidth = Math.max(4, ...results.map((r) => r.name.length));
  const verWidth = Math.max(7, ...results.map((r) => (r.version || '').length));

  console.log(
    `${'NAME'.padEnd(nameWidth)}  ${'VERSION'.padEnd(verWidth)}  DESCRIPTION`
  );
  for (const r of results) {
    console.log(
      `${r.name.padEnd(nameWidth)}  ${(r.version || '').padEnd(verWidth)}  ${r.description || ''}`
    );
  }
}

module.exports = { search };

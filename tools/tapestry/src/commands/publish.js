'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const fetch = require('node-fetch');
const FormData = require('form-data');
const { readYaml } = require('../util/yaml');
const { validate } = require('./validate');
const { buildTarball, computeIntegrity } = require('../lib/tarball-builder');
const { requireToken } = require('../lib/auth');
const { DEFAULT_REGISTRY } = require('../lib/registry-client');

async function publish({ cwd = process.cwd(), registryUrl = DEFAULT_REGISTRY } = {}) {
  validate({ cwd });

  const manifest = readYaml(path.join(cwd, 'tapestry.yaml'));
  const token = requireToken();

  const shortName = manifest.name.split('/')[1];
  const tmpPath = path.join(
    os.tmpdir(),
    `tapestry-publish-${shortName}-${manifest.version}.tgz`
  );

  try {
    console.log(`Publishing ${manifest.name}@${manifest.version}...`);
    await buildTarball(cwd, tmpPath);

    const integrity = computeIntegrity(tmpPath);

    const form = new FormData();
    form.append('tarball', fs.createReadStream(tmpPath), {
      filename: `${manifest.version}.tgz`,
      contentType: 'application/gzip',
    });
    form.append('metadata', JSON.stringify({ ...manifest, integrity }));

    const res = await fetch(`${registryUrl}/v1/publish`, {
      method: 'POST',
      headers: {
        ...form.getHeaders(),
        Authorization: `Bearer ${token}`,
      },
      body: form,
    });

    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      throw new Error(body.error || `Publish failed (${res.status})`);
    }

    const result = await res.json();
    console.log(`  Published ${result.name}@${result.version}`);
    console.log('Done.');
  } finally {
    if (fs.existsSync(tmpPath)) {
      fs.unlinkSync(tmpPath);
    }
  }
}

module.exports = { publish };

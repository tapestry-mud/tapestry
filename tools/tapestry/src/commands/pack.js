'use strict';

const path = require('path');
const { readYaml } = require('../util/yaml');
const { buildTarball, computeIntegrity } = require('../lib/tarball-builder');
const { validate } = require('./validate');

async function pack({ cwd = process.cwd() } = {}) {
  validate({ cwd });

  const manifest = readYaml(path.join(cwd, 'tapestry.yaml'));
  const shortName = manifest.name.split('/')[1];
  const outputPath = path.join(cwd, `${shortName}-${manifest.version}.tgz`);

  console.log(`Packing ${manifest.name}@${manifest.version}...`);
  await buildTarball(cwd, outputPath);

  const integrity = computeIntegrity(outputPath);
  console.log(`  ${path.basename(outputPath)}`);
  console.log(`  integrity: ${integrity}`);
  console.log('Done.');
}

module.exports = { pack };

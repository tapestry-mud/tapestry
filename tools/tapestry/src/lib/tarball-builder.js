'use strict';

const crypto = require('crypto');
const fs = require('fs');
const tar = require('tar');

const EXCLUDE = new Set(['.git', 'node_modules', '.DS_Store']);

function shouldInclude(filePath) {
  if (filePath.endsWith('.tgz')) {
    return false;
  }
  return !filePath.split('/').some((part) => EXCLUDE.has(part));
}

async function buildTarball(packDir, outputPath) {
  await tar.create(
    {
      file: outputPath,
      gzip: true,
      cwd: packDir,
      prefix: 'package',
      filter: (p) => shouldInclude(p),
    },
    ['.']
  );
}

function computeIntegrity(filePath) {
  const data = fs.readFileSync(filePath);
  const hash = crypto.createHash('sha256').update(data).digest('base64');
  return `sha256-${hash}`;
}

module.exports = { buildTarball, computeIntegrity };

'use strict';

const crypto = require('crypto');
const fs = require('fs');
const tar = require('tar');

function verifyIntegrity(buffer, expected) {
  const hash = crypto.createHash('sha256').update(buffer).digest('base64');
  const computed = `sha256-${hash}`;
  if (computed !== expected) {
    throw new Error(
      `Integrity check failed\n  expected: ${expected}\n  got:      ${computed}`
    );
  }
}

function saveTarball(buffer, destPath) {
  fs.writeFileSync(destPath, buffer);
}

async function extractTarball(tarballPath, destDir) {
  fs.mkdirSync(destDir, { recursive: true });
  await tar.extract({ file: tarballPath, cwd: destDir, strip: 1 });
}

module.exports = { verifyIntegrity, saveTarball, extractTarball };

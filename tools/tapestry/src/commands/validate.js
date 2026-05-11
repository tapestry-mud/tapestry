'use strict';

const fs = require('fs');
const path = require('path');
const { readYaml } = require('../util/yaml');
const { validatePackageManifest } = require('../schema/manifest');

function validate({ cwd = process.cwd() } = {}) {
  const manifestPath = path.join(cwd, 'tapestry.yaml');
  if (!fs.existsSync(manifestPath)) {
    throw new Error('No tapestry.yaml found in current directory');
  }

  const data = readYaml(manifestPath);
  console.log(`Validating ${manifestPath}...`);

  const result = validatePackageManifest(data);
  if (!result.success) {
    for (const issue of result.error.issues) {
      const fieldPath = issue.path.join('.') || 'root';
      console.log(`  error: ${fieldPath} - ${issue.message}`);
    }
    throw new Error(`${result.error.issues.length} validation error(s)`);
  }

  console.log(`  OK  ${data.name} v${data.version}`);
}

module.exports = { validate };

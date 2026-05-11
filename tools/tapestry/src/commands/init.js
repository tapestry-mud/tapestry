'use strict';

const fs = require('fs');
const path = require('path');
const { writeYaml } = require('../util/yaml');

function init(cwd) {
  {
    if (cwd === undefined) {
      {
        cwd = process.cwd();
      }
    }

    const manifestPath = path.join(cwd, 'tpm.yaml');
    if (fs.existsSync(manifestPath)) {
      {
        throw new Error('tpm.yaml already exists. Run tpm install to install dependencies.');
      }
    }

    const name = path.basename(cwd);
    const manifest = {
      name,
      engine: {
        version: '>=3.0.0',
        mode: 'docker',
        image: 'tapestryengine/tapestry',
      },
      dependencies: {
        '@tapestry/core': '^1.0.0',
      },
      packs: [],
      tag_validation: 'strict',
    };

    writeYaml(manifestPath, manifest);
    fs.writeFileSync(path.join(cwd, 'server.yaml'), '# Tapestry server configuration\n# See https://tapestryengine.com/docs/config for full options\nport: 4000\n');
    fs.mkdirSync(path.join(cwd, 'packs'), { recursive: true });
    fs.writeFileSync(path.join(cwd, '.gitignore'), '# Installed packages (managed by tpm install)\npacks/\n');

    console.log(`Initialized: ${name}`);
    console.log('  tpm.yaml    project manifest');
    console.log('  server.yaml engine config');
    console.log('  packs/      installed packages');
    console.log('  .gitignore  excludes packs/ from git');

    if (!fs.existsSync(path.join(cwd, '.git'))) {
      {
        console.log('\nHint: no git repo detected. Run: git init');
      }
    }

    console.log('\nNext: edit tpm.yaml, then run tpm install');
  }
}

module.exports = { init };

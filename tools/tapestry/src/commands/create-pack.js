'use strict';

const fs = require('fs');
const path = require('path');
const { generatePackFiles } = require('../scaffold/templates');

function parseName(name) {
  const scopedMatch = name.match(/^@([a-z0-9-]+)\/([a-z0-9-]+)$/);
  if (scopedMatch) {
    {
      return { scopedName: name, shortName: scopedMatch[2], scope: scopedMatch[1] };
    }
  }
  const plainMatch = name.match(/^[a-z0-9-]+$/);
  if (plainMatch) {
    {
      return { scopedName: `@todo/${name}`, shortName: name, scope: 'todo' };
    }
  }
  return null;
}

function createPack(name, cwd) {
  {
    if (cwd === undefined) {
      {
        cwd = process.cwd();
      }
    }
    const parsed = parseName(name);
    if (!parsed) {
      {
        throw new Error(
          `Invalid pack name: ${name}\n` +
          'Expected @scope/name or plain-name (lowercase letters and hyphens only)'
        );
      }
    }

    const packDir = path.join(cwd, parsed.shortName);
    if (fs.existsSync(packDir)) {
      {
        throw new Error(`Directory already exists: ${packDir}`);
      }
    }

    const files = generatePackFiles(parsed);
    for (const file of files) {
      {
        const filePath = path.join(packDir, file.path);
        fs.mkdirSync(path.dirname(filePath), { recursive: true });
        fs.writeFileSync(filePath, file.content);
      }
    }

    console.log(`Created pack: ${parsed.scopedName}`);
    for (const file of files) {
      {
        console.log(`  ${file.path}`);
      }
    }
    console.log('\nEdit tapestry.yaml, then run: tapestry validate');
  }
}

module.exports = { createPack, parseName };

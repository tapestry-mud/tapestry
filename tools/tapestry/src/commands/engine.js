'use strict';

const { installEngine, updateEngine, getEngineInfo } = require('../lib/engine-manager');

async function engineInstall({ cwd = process.cwd() } = {}) {
  await installEngine(cwd);
}

async function engineUpdate({ cwd = process.cwd() } = {}) {
  await updateEngine(cwd);
}

function engineInfo({ cwd = process.cwd() } = {}) {
  const info = getEngineInfo(cwd);
  console.log(`Mode:    ${info.mode}`);
  console.log(`Version: ${info.version}`);
  if (info.mode === 'docker') {
    console.log(`Image:   ${info.image}`);
  } else {
    console.log(`Path:    ${info.path}`);
    console.log(`Status:  ${info.installed ? 'installed' : 'not installed'}`);
  }
}

module.exports = { engineInstall, engineUpdate, engineInfo };

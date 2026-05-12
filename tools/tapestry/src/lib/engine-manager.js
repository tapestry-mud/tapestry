'use strict';

const fs = require('fs');
const path = require('path');
const { spawnSync, spawn } = require('child_process');
const { readYaml } = require('../util/yaml');
const { writePid, readPid, clearPid } = require('./process-tracker');

const ENGINE_REPO = 'https://github.com/tapestry-mud/tapestry.git';
const DEFAULT_IMAGE = 'ghcr.io/tapestry-mud/tapestry';
const PLATFORM_MAP = { linux: 'linux', darwin: 'osx', win32: 'windows' };

function readEngineConfig(cwd) {
  const manifestPath = path.join(cwd, 'tapestry.yaml');
  if (!fs.existsSync(manifestPath)) {
    throw new Error('No tapestry.yaml found. Run tapestry init first.');
  }
  const manifest = readYaml(manifestPath);
  const engine = manifest.engine;
  if (!engine || typeof engine !== 'object') {
    throw new Error(
      'engine must be configured as an object in tapestry.yaml:\n' +
      '  engine:\n    version: "3.1.0"\n    mode: "docker"'
    );
  }
  if (!engine.version) {
    throw new Error('engine.version is required in tapestry.yaml');
  }
  if (!['docker', 'binary', 'source'].includes(engine.mode)) {
    throw new Error(
      `engine.mode must be docker, binary, or source. Got: ${engine.mode}`
    );
  }
  return {
    version: engine.version,
    mode: engine.mode,
    image: engine.image || DEFAULT_IMAGE,
    installDir: path.join(cwd, '.tapestry-engine'),
    projectName: (manifest.name || 'tapestry').replace(/[^a-z0-9-]/g, '-'),
  };
}

async function installEngine(cwd) {
  const config = readEngineConfig(cwd);
  void config;
}

async function updateEngine(cwd) {
  const config = readEngineConfig(cwd);
  void config;
}

function getEngineInfo(cwd) {
  return readEngineConfig(cwd);
}

async function startEngine(cwd) {
  const config = readEngineConfig(cwd);
  void config;
}

async function stopEngine(cwd) {
  const config = readEngineConfig(cwd);
  void config;
}

module.exports = { installEngine, updateEngine, getEngineInfo, startEngine, stopEngine };

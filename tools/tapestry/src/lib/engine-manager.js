'use strict';

const fs = require('fs');
const path = require('path');
const { spawnSync, spawn } = require('child_process');
const { readYaml } = require('../util/yaml');
const { writePid, readPid, clearPid } = require('./process-tracker');

const ENGINE_REPO = 'https://github.com/tapestry-mud/tapestry.git';
const DEFAULT_IMAGE = 'ghcr.io/tapestry-mud/tapestry';
const PLATFORM_MAP = { linux: 'linux', darwin: 'osx', win32: 'windows' };

// ── Docker helpers ──────────────────────────────────────────────────────────

function dockerPull(image, version) {
  console.log(`Pulling ${image}:${version}...`);
  const result = spawnSync('docker', ['pull', `${image}:${version}`], { stdio: 'inherit' });
  if (result.status !== 0) {
    console.log(`Tag ${version} not found, falling back to ${image}:latest...`);
    const fallback = spawnSync('docker', ['pull', `${image}:latest`], { stdio: 'inherit' });
    if (fallback.status !== 0) {
      throw new Error(
        `docker pull failed. Is Docker running and is ${image} a valid image?`
      );
    }
    spawnSync('docker', ['tag', `${image}:latest`, `${image}:${version}`], { stdio: 'inherit' });
    console.log(`Engine image ready: ${image}:${version} (from latest)`);
    return;
  }
  console.log(`Engine image ready: ${image}:${version}`);
}

function dockerStart(projectName, image, version, packsDir, serverYamlPath) {
  const containerName = `tapestry-${projectName}`;
  const result = spawnSync('docker', [
    'run', '--detach',
    '--name', containerName,
    '-p', '4000:4000',
    '-p', '4001:4001',
    '-v', `${packsDir}:/app/packs`,
    '-v', `${serverYamlPath}:/app/server.yaml`,
    `${image}:${version}`,
  ], { stdio: 'inherit' });
  if (result.status !== 0) {
    throw new Error(
      `docker run failed. Ensure the image exists and no container named '${containerName}' is already running.`
    );
  }
  console.log(`Engine started. Container: ${containerName}`);
  console.log('  Telnet:    telnet localhost 4000');
  console.log('  WebSocket: ws://localhost:4001');
}

function dockerStop(projectName) {
  const containerName = `tapestry-${projectName}`;
  const result = spawnSync('docker', ['stop', containerName], { stdio: 'inherit' });
  if (result.status !== 0) {
    throw new Error(`Failed to stop container '${containerName}'. Is it running?`);
  }
  const rmResult = spawnSync('docker', ['rm', containerName], { stdio: 'inherit' });
  if (rmResult.status !== 0) {
    throw new Error(`Failed to remove container '${containerName}'.`);
  }
  console.log('Engine stopped.');
}

function dockerInfo(image, version) {
  return { mode: 'docker', version, image: `${image}:${version}` };
}

// ── Binary helpers ──────────────────────────────────────────────────────────

function binaryExecName() {
  return process.platform === 'win32' ? 'Tapestry.exe' : 'Tapestry';
}

function binaryInstall(version, installDir) {
  const platform = PLATFORM_MAP[process.platform] || 'linux';
  const url =
    `https://github.com/tapestry-mud/tapestry/releases/download/v${version}/tapestry-${platform}.tar.gz`;
  const binDir = path.join(installDir, 'binary', version);
  fs.mkdirSync(binDir, { recursive: true });
  const tarPath = path.join(binDir, 'tapestry.tar.gz');

  console.log(`Downloading Tapestry engine v${version} for ${platform}...`);
  const dlResult = spawnSync('curl', ['-L', '-o', tarPath, url], { stdio: 'inherit' });
  if (dlResult.status !== 0) {
    throw new Error(
      'Failed to download engine binary. Ensure curl is installed and the version exists on GitHub releases.'
    );
  }

  const exResult = spawnSync('tar', ['-xzf', tarPath, '-C', binDir], { stdio: 'inherit' });
  if (exResult.status !== 0) {
    if (fs.existsSync(tarPath)) {
      fs.unlinkSync(tarPath);
    }
    throw new Error('Failed to extract engine binary.');
  }

  if (fs.existsSync(tarPath)) {
    fs.unlinkSync(tarPath);
  }
  console.log(`Engine installed to ${binDir}`);
}

function binaryStart(version, installDir, packsDir, serverYamlPath, cwd) {
  const binDir = path.join(installDir, 'binary', version);
  const execPath = path.join(binDir, binaryExecName());
  if (!fs.existsSync(execPath)) {
    throw new Error(
      `Engine binary not found at ${execPath}. Run tapestry engine install first.`
    );
  }
  const child = spawn(execPath, ['--packs', packsDir, '--config', serverYamlPath], {
    detached: true,
    stdio: 'ignore',
  });
  child.unref();
  writePid(cwd, child.pid);
  console.log(`Engine started (PID ${child.pid}).`);
  console.log('  Telnet:    telnet localhost 4000');
  console.log('  WebSocket: ws://localhost:4001');
}

function binaryInfo(version, installDir) {
  const binDir = path.join(installDir, 'binary', version);
  return { mode: 'binary', version, path: binDir, installed: fs.existsSync(binDir) };
}

// ── Source helpers ──────────────────────────────────────────────────────────

function sourceInstall(installDir) {
  const sourceDir = path.join(installDir, 'source');
  if (fs.existsSync(sourceDir)) {
    throw new Error(
      `Engine source already exists at ${sourceDir}. Run tapestry engine update to pull changes.`
    );
  }
  console.log('Cloning Tapestry engine source...');
  const result = spawnSync('git', ['clone', ENGINE_REPO, sourceDir], { stdio: 'inherit' });
  if (result.status !== 0) {
    throw new Error('git clone failed. Check your network connection.');
  }
  console.log(`Engine source cloned to ${sourceDir}`);
}

function sourcePull(installDir) {
  const sourceDir = path.join(installDir, 'source');
  if (!fs.existsSync(sourceDir)) {
    throw new Error('Engine source not found. Run tapestry engine install first.');
  }
  console.log('Pulling engine source updates...');
  const result = spawnSync('git', ['-C', sourceDir, 'pull'], { stdio: 'inherit' });
  if (result.status !== 0) {
    throw new Error('git pull failed. Check your network connection.');
  }
  console.log('Engine source updated.');
}

function sourceStart(installDir, packsDir, serverYamlPath, cwd) {
  const sourceDir = path.join(installDir, 'source');
  if (!fs.existsSync(sourceDir)) {
    throw new Error('Engine source not found. Run tapestry engine install first.');
  }
  const child = spawn(
    'dotnet',
    ['run', '--', '--packs', packsDir, '--config', serverYamlPath],
    { cwd: sourceDir, detached: true, stdio: 'ignore' }
  );
  child.unref();
  writePid(cwd, child.pid);
  console.log(`Engine started via dotnet run (PID ${child.pid}).`);
  console.log('  Telnet:    telnet localhost 4000');
  console.log('  WebSocket: ws://localhost:4001');
}

function sourceInfo(version, installDir) {
  const sourceDir = path.join(installDir, 'source');
  return { mode: 'source', version, path: sourceDir, installed: fs.existsSync(sourceDir) };
}

// ── Shared process helpers ──────────────────────────────────────────────────

function processStop(cwd) {
  const pid = readPid(cwd);
  if (!pid) {
    throw new Error('Engine is not running (no .tapestry.pid found).');
  }
  try {
    process.kill(pid, 'SIGTERM');
  } catch (_e) {
    // process already gone — clear the pid file and report success
  }
  clearPid(cwd);
  console.log('Engine stopped.');
}

// ── Config reader ───────────────────────────────────────────────────────────

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
    projectName: (manifest.name || 'tapestry').toLowerCase().replace(/[^a-z0-9-]+/g, '-'),
  };
}

// ── Public API ──────────────────────────────────────────────────────────────

async function installEngine(cwd) {
  const config = readEngineConfig(cwd);
  if (config.mode === 'docker') {
    dockerPull(config.image, config.version);
  } else if (config.mode === 'binary') {
    binaryInstall(config.version, config.installDir);
  } else {
    sourceInstall(config.installDir);
  }
}

async function updateEngine(cwd) {
  const config = readEngineConfig(cwd);
  if (config.mode === 'docker') {
    dockerPull(config.image, config.version);
  } else if (config.mode === 'binary') {
    binaryInstall(config.version, config.installDir);
  } else {
    sourcePull(config.installDir);
  }
}

function getEngineInfo(cwd) {
  const config = readEngineConfig(cwd);
  if (config.mode === 'docker') {
    return dockerInfo(config.image, config.version);
  }
  if (config.mode === 'binary') {
    return binaryInfo(config.version, config.installDir);
  }
  return sourceInfo(config.version, config.installDir);
}

async function startEngine(cwd) {
  const config = readEngineConfig(cwd);
  const packsDir = path.resolve(cwd, 'packs');
  const serverYamlPath = path.resolve(cwd, 'server.yaml');
  if (!fs.existsSync(packsDir)) {
    throw new Error('packs/ directory not found. Run tapestry install first.');
  }
  if (!fs.existsSync(serverYamlPath)) {
    throw new Error('server.yaml not found in the current directory.');
  }
  if (config.mode === 'docker') {
    dockerStart(config.projectName, config.image, config.version, packsDir, serverYamlPath);
  } else if (config.mode === 'binary') {
    binaryStart(config.version, config.installDir, packsDir, serverYamlPath, cwd);
  } else {
    sourceStart(config.installDir, packsDir, serverYamlPath, cwd);
  }
}

async function stopEngine(cwd) {
  const config = readEngineConfig(cwd);
  if (config.mode === 'docker') {
    dockerStop(config.projectName);
  } else {
    processStop(cwd);
  }
}

module.exports = { installEngine, updateEngine, getEngineInfo, startEngine, stopEngine };

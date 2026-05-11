'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { writeYaml } = require('../../src/util/yaml');
const { list } = require('../../src/commands/list');

let tmpDir;

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-list-'));
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

function writeLock(cwd, packages) {
  const resolved = {};
  for (const [name, version] of Object.entries(packages)) {
    resolved[name] = { version, integrity: 'sha256-x', tarball: 'http://example.com' };
  }
  writeYaml(path.join(cwd, 'tapestry-lock.yaml'), { lockfile_version: 1, resolved });
}

function writeBoot(cwd, packs) {
  writeYaml(path.join(cwd, 'tapestry-boot.yaml'), { modules: [], packs });
}

function writePackManifest(cwd, packageName, data) {
  const parts = packageName.split('/');
  const packDir = path.join(cwd, 'packs', ...parts);
  fs.mkdirSync(packDir, { recursive: true });
  writeYaml(path.join(packDir, 'tapestry.yaml'), data);
}

it('prints "No packages installed." when no lock file', async () => {
  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  await list({ cwd: tmpDir });
  expect(spy).toHaveBeenCalledWith('No packages installed.');
  spy.mockRestore();
});

it('prints a row for each installed package', async () => {
  writeLock(tmpDir, { '@tapestry/core': '1.0.0', '@tapestry/weather': '0.8.1' });
  writeBoot(tmpDir, { '@tapestry/core': { enabled: true }, '@tapestry/weather': { enabled: true } });

  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  await list({ cwd: tmpDir });

  const output = spy.mock.calls.map((c) => c[0]).join('\n');
  expect(output).toContain('@tapestry/core');
  expect(output).toContain('1.0.0');
  expect(output).toContain('@tapestry/weather');
  expect(output).toContain('0.8.1');
  spy.mockRestore();
});

it('shows "disabled" for packages with enabled: false in boot', async () => {
  writeLock(tmpDir, { '@tapestry/weather': '0.8.1' });
  writeBoot(tmpDir, { '@tapestry/weather': { enabled: false } });

  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  await list({ cwd: tmpDir });

  const output = spy.mock.calls.map((c) => c[0]).join('\n');
  expect(output).toContain('disabled');
  spy.mockRestore();
});

it('shows "enabled" for packages with enabled: true in boot', async () => {
  writeLock(tmpDir, { '@tapestry/core': '1.0.0' });
  writeBoot(tmpDir, { '@tapestry/core': { enabled: true } });

  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  await list({ cwd: tmpDir });

  const output = spy.mock.calls.map((c) => c[0]).join('\n');
  expect(output).toContain('enabled');
  spy.mockRestore();
});

it('shows pack type from installed pack manifest when available', async () => {
  writeLock(tmpDir, { '@tapestry/weather': '0.8.1' });
  writeBoot(tmpDir, { '@tapestry/weather': { enabled: true } });
  writePackManifest(tmpDir, '@tapestry/weather', { name: '@tapestry/weather', version: '0.8.1', type: 'module' });

  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  await list({ cwd: tmpDir });

  const output = spy.mock.calls.map((c) => c[0]).join('\n');
  expect(output).toContain('module');
  spy.mockRestore();
});

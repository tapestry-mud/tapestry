'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');

jest.mock('../../src/lib/registry-client');

const { fetchPackageMetadata } = require('../../src/lib/registry-client');
const { writeYaml } = require('../../src/util/yaml');
const { outdated } = require('../../src/commands/outdated');

let tmpDir;

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-outdated-'));
  jest.clearAllMocks();
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

function registryMeta(name, latestVersion) {
  return { name, owner: 'mallek', versions: [{ version: latestVersion, manifest: {} }] };
}

it('prints "No packages installed." when no lock file', async () => {
  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  await outdated({ cwd: tmpDir });
  expect(spy).toHaveBeenCalledWith('No packages installed.');
  spy.mockRestore();
});

it('prints "All packages are up to date." when nothing is stale', async () => {
  writeLock(tmpDir, { '@tapestry/core': '1.0.0' });
  fetchPackageMetadata.mockResolvedValue(registryMeta('@tapestry/core', '1.0.0'));

  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  await outdated({ cwd: tmpDir });
  expect(spy).toHaveBeenCalledWith('All packages are up to date.');
  spy.mockRestore();
});

it('lists packages with newer versions available', async () => {
  writeLock(tmpDir, { '@tapestry/weather': '0.8.0', '@tapestry/core': '1.0.0' });
  fetchPackageMetadata
    .mockResolvedValueOnce(registryMeta('@tapestry/weather', '0.8.1'))
    .mockResolvedValueOnce(registryMeta('@tapestry/core', '1.0.0'));

  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  await outdated({ cwd: tmpDir });

  const output = spy.mock.calls.map((c) => c[0]).join('\n');
  expect(output).toContain('@tapestry/weather');
  expect(output).toContain('0.8.0');
  expect(output).toContain('0.8.1');
  expect(output).not.toContain('@tapestry/core');
  spy.mockRestore();
});

it('skips packages where registry lookup fails', async () => {
  writeLock(tmpDir, { '@tapestry/gone': '1.0.0', '@tapestry/core': '1.0.0' });
  fetchPackageMetadata
    .mockRejectedValueOnce(new Error('Package @tapestry/gone not found'))
    .mockResolvedValueOnce(registryMeta('@tapestry/core', '1.0.0'));

  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  await expect(outdated({ cwd: tmpDir })).resolves.not.toThrow();
  spy.mockRestore();
});

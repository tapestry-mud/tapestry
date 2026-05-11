'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { uninstall } = require('../../src/commands/uninstall');
const { readYaml, writeYaml } = require('../../src/util/yaml');
const { writeLock, readLock } = require('../../src/lib/lock-file');
const { writeBoot, readBoot } = require('../../src/lib/boot');

let tmpDir;

function setupProject(cwd, deps = {}, installedPackages = []) {
  writeYaml(path.join(cwd, 'tapestry.yaml'), { name: 'test-game', engine: '>=3.0.0', dependencies: deps });
  fs.mkdirSync(path.join(cwd, 'packs'), { recursive: true });

  const resolved = {};
  for (const pkg of installedPackages) {
    const parts = pkg.split('/');
    const pkgDir = path.join(cwd, 'packs', ...parts);
    fs.mkdirSync(pkgDir, { recursive: true });
    writeYaml(path.join(pkgDir, 'tapestry.yaml'), { name: pkg, version: '1.0.0' });
    resolved[pkg] = { version: '1.0.0', integrity: 'sha256-x', tarball: 'http://example.com/a.tgz' };
  }
  writeLock(cwd, { lockfile_version: 1, resolved });
}

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-uninstall-'));
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

describe('uninstall', () => {
  it('throws when tapestry.yaml is missing', async () => {
    await expect(uninstall('@tapestry/weather', { cwd: tmpDir })).rejects.toThrow('No tapestry.yaml found');
  });

  it('throws when package is not in dependencies', async () => {
    setupProject(tmpDir, { '@tapestry/core': '^1.0.0' });
    await expect(uninstall('@tapestry/weather', { cwd: tmpDir })).rejects.toThrow(
      '@tapestry/weather is not in tapestry.yaml'
    );
  });

  it('removes package directory from packs/', async () => {
    setupProject(tmpDir, { '@tapestry/weather': '^0.8.0' }, ['@tapestry/weather']);

    await uninstall('@tapestry/weather', { cwd: tmpDir });

    expect(fs.existsSync(path.join(tmpDir, 'packs', '@tapestry', 'weather'))).toBe(false);
  });

  it('removes package from tapestry.yaml dependencies', async () => {
    setupProject(tmpDir, { '@tapestry/weather': '^0.8.0' }, ['@tapestry/weather']);

    await uninstall('@tapestry/weather', { cwd: tmpDir });

    const manifest = readYaml(path.join(tmpDir, 'tapestry.yaml'));
    expect(manifest.dependencies['@tapestry/weather']).toBeUndefined();
  });

  it('removes package from lock file', async () => {
    setupProject(tmpDir, { '@tapestry/weather': '^0.8.0' }, ['@tapestry/weather']);

    await uninstall('@tapestry/weather', { cwd: tmpDir });

    const lock = readLock(tmpDir);
    expect(lock.resolved['@tapestry/weather']).toBeUndefined();
  });

  it('removes package from boot file', async () => {
    setupProject(tmpDir, { '@tapestry/weather': '^0.8.0' }, ['@tapestry/weather']);
    writeBoot(tmpDir, { modules: [], packs: { '@tapestry/weather': { enabled: true } } });

    await uninstall('@tapestry/weather', { cwd: tmpDir });

    const boot = readBoot(tmpDir);
    expect(boot.packs['@tapestry/weather']).toBeUndefined();
  });

  it('succeeds even if pack directory does not exist on disk', async () => {
    setupProject(tmpDir, { '@tapestry/weather': '^0.8.0' });

    await expect(uninstall('@tapestry/weather', { cwd: tmpDir })).resolves.not.toThrow();
  });
});

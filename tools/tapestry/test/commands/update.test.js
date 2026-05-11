'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');

jest.mock('../../src/lib/registry-client');
jest.mock('../../src/lib/tarball');

const { fetchPackageMetadata, fetchTarball } = require('../../src/lib/registry-client');
const { verifyIntegrity, saveTarball, extractTarball } = require('../../src/lib/tarball');

const { update } = require('../../src/commands/update');
const { writeYaml } = require('../../src/util/yaml');
const { writeLock, readLock } = require('../../src/lib/lock-file');

let tmpDir;

function makeRegistryMeta(name, version, deps = {}) {
  return {
    name,
    versions: [{
      version,
      integrity: `sha256-new-${version}`,
      manifest: { name, version, dependencies: deps },
    }],
  };
}

function setupProject(cwd, deps, lockedVersions) {
  writeYaml(path.join(cwd, 'tapestry.yaml'), { name: 'test-game', engine: '>=3.0.0', dependencies: deps });
  fs.mkdirSync(path.join(cwd, 'packs'), { recursive: true });
  if (lockedVersions) {
    const resolved = {};
    for (const [name, version] of Object.entries(lockedVersions)) {
      resolved[name] = { version, integrity: 'sha256-old', tarball: `http://example.com/${name}/${version}.tgz` };
      const parts = name.split('/');
      const pkgDir = path.join(cwd, 'packs', ...parts);
      fs.mkdirSync(pkgDir, { recursive: true });
      writeYaml(path.join(pkgDir, 'tapestry.yaml'), { name, version });
    }
    writeLock(cwd, { lockfile_version: 1, resolved });
  }
}

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-update-'));
  fetchPackageMetadata.mockReset();
  fetchTarball.mockReset();
  verifyIntegrity.mockReset();
  saveTarball.mockReset();
  extractTarball.mockReset();
  extractTarball.mockResolvedValue(undefined);
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

describe('update', () => {
  it('throws when tapestry.yaml is missing', async () => {
    await expect(update(undefined, { cwd: tmpDir })).rejects.toThrow('No tapestry.yaml found');
  });

  it('re-resolves and downloads when a newer version is available', async () => {
    setupProject(tmpDir, { '@tapestry/core': '^1.0.0' }, { '@tapestry/core': '1.0.0' });
    fetchPackageMetadata.mockResolvedValue(makeRegistryMeta('@tapestry/core', '1.1.0'));
    fetchTarball.mockResolvedValue(Buffer.from('new'));
    extractTarball.mockImplementation(async (_tarPath, destDir) => {
      fs.mkdirSync(destDir, { recursive: true });
      writeYaml(path.join(destDir, 'tapestry.yaml'), { name: '@tapestry/core', version: '1.1.0' });
    });

    await update(undefined, { cwd: tmpDir });

    const lock = readLock(tmpDir);
    expect(lock.resolved['@tapestry/core'].version).toBe('1.1.0');
    expect(extractTarball).toHaveBeenCalled();
  });

  it('skips download when version is already current', async () => {
    setupProject(tmpDir, { '@tapestry/core': '^1.0.0' }, { '@tapestry/core': '1.0.0' });
    fetchPackageMetadata.mockResolvedValue(makeRegistryMeta('@tapestry/core', '1.0.0'));

    await update(undefined, { cwd: tmpDir });

    expect(extractTarball).not.toHaveBeenCalled();
  });

  it('updates only the named package when arg is given', async () => {
    setupProject(
      tmpDir,
      { '@tapestry/core': '^1.0.0', '@tapestry/weather': '^0.8.0' },
      { '@tapestry/core': '1.0.0', '@tapestry/weather': '0.8.0' }
    );
    fetchPackageMetadata.mockResolvedValue(makeRegistryMeta('@tapestry/weather', '0.8.5'));
    fetchTarball.mockResolvedValue(Buffer.from('new'));
    extractTarball.mockImplementation(async (_tarPath, destDir) => {
      fs.mkdirSync(destDir, { recursive: true });
      writeYaml(path.join(destDir, 'tapestry.yaml'), { name: '@tapestry/weather', version: '0.8.5' });
    });

    await update('@tapestry/weather', { cwd: tmpDir });

    const lock = readLock(tmpDir);
    expect(lock.resolved['@tapestry/weather'].version).toBe('0.8.5');
    expect(fetchPackageMetadata).toHaveBeenCalledWith('@tapestry/weather', expect.any(String));
    expect(fetchPackageMetadata).toHaveBeenCalledTimes(1);
  });

  it('throws when named package is not in tapestry.yaml', async () => {
    setupProject(tmpDir, { '@tapestry/core': '^1.0.0' }, {});
    await expect(update('@tapestry/missing', { cwd: tmpDir })).rejects.toThrow(
      '@tapestry/missing is not in tapestry.yaml'
    );
  });
});

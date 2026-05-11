'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');

jest.mock('../../src/lib/registry-client');
jest.mock('../../src/lib/tarball');

const { fetchPackageMetadata, fetchTarball } = require('../../src/lib/registry-client');
const { verifyIntegrity, saveTarball, extractTarball } = require('../../src/lib/tarball');

const { install } = require('../../src/commands/install');
const { readYaml, writeYaml } = require('../../src/util/yaml');
const { readLock, writeLock } = require('../../src/lib/lock-file');
const { readBoot } = require('../../src/lib/boot');

let tmpDir;

function makeRegistryMeta(name, version, deps = {}) {
  return {
    name,
    versions: [{
      version,
      integrity: `sha256-fake-${version}`,
      manifest: { name, version, dependencies: deps },
    }],
  };
}

function writeProjectManifest(cwd, deps = {}) {
  writeYaml(path.join(cwd, 'tapestry.yaml'), {
    name: 'test-game',
    engine: '>=3.0.0',
    dependencies: deps,
  });
  fs.mkdirSync(path.join(cwd, 'packs'), { recursive: true });
}

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-install-'));
  fetchPackageMetadata.mockReset();
  fetchTarball.mockReset();
  verifyIntegrity.mockReset();
  saveTarball.mockReset();
  extractTarball.mockReset();
  // Default: write a minimal pack manifest so installResolved can read it after extraction.
  extractTarball.mockImplementation(async (_tarPath, destDir) => {
    fs.mkdirSync(destDir, { recursive: true });
    writeYaml(path.join(destDir, 'tapestry.yaml'), { name: 'mock-pkg', version: '1.0.0' });
  });
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

describe('install (no args)', () => {
  it('throws when tapestry.yaml is missing', async () => {
    await expect(install(undefined, { cwd: tmpDir })).rejects.toThrow('No tapestry.yaml found');
  });

  it('installs all dependencies from tapestry.yaml when no lock file exists', async () => {
    writeProjectManifest(tmpDir, { '@tapestry/core': '^1.0.0' });
    fetchPackageMetadata.mockResolvedValue(makeRegistryMeta('@tapestry/core', '1.0.0'));
    fetchTarball.mockResolvedValue(Buffer.from('fake'));

    await install(undefined, { cwd: tmpDir });

    const lock = readLock(tmpDir);
    expect(lock.resolved['@tapestry/core'].version).toBe('1.0.0');
    expect(extractTarball).toHaveBeenCalledTimes(1);
  });

  it('uses lock file for fast install when lock matches manifest deps', async () => {
    writeProjectManifest(tmpDir, { '@tapestry/core': '^1.0.0' });
    writeLock(tmpDir, {
      lockfile_version: 1,
      resolved: {
        '@tapestry/core': {
          version: '1.0.0',
          integrity: 'sha256-abc',
          tarball: 'http://localhost:3002/v1/packages/@tapestry/core/1.0.0.tgz',
        },
      },
    });
    fetchTarball.mockResolvedValue(Buffer.from('fake'));

    await install(undefined, { cwd: tmpDir });

    expect(fetchPackageMetadata).not.toHaveBeenCalled();
    expect(extractTarball).toHaveBeenCalledTimes(1);
  });

  it('re-resolves when manifest has a dep not in the lock file', async () => {
    writeProjectManifest(tmpDir, { '@tapestry/core': '^1.0.0', '@tapestry/weather': '^0.8.0' });
    // Lock only has core, missing weather
    writeLock(tmpDir, {
      lockfile_version: 1,
      resolved: {
        '@tapestry/core': {
          version: '1.0.0',
          integrity: 'sha256-abc',
          tarball: 'http://localhost:3002/v1/packages/@tapestry/core/1.0.0.tgz',
        },
      },
    });
    fetchPackageMetadata
      .mockResolvedValueOnce(makeRegistryMeta('@tapestry/core', '1.0.0'))
      .mockResolvedValueOnce(makeRegistryMeta('@tapestry/weather', '0.8.1'));
    fetchTarball.mockResolvedValue(Buffer.from('fake'));

    await install(undefined, { cwd: tmpDir });

    expect(fetchPackageMetadata).toHaveBeenCalled();
    const lock = readLock(tmpDir);
    expect(lock.resolved['@tapestry/weather']).toBeDefined();
  });

  it('skips already-installed packages', async () => {
    writeProjectManifest(tmpDir, { '@tapestry/core': '^1.0.0' });
    writeLock(tmpDir, {
      lockfile_version: 1,
      resolved: {
        '@tapestry/core': {
          version: '1.0.0',
          integrity: 'sha256-abc',
          tarball: 'http://localhost:3002/v1/packages/@tapestry/core/1.0.0.tgz',
        },
      },
    });
    // Pre-create the install dir to simulate already installed
    fs.mkdirSync(path.join(tmpDir, 'packs', '@tapestry', 'core'), { recursive: true });
    fs.writeFileSync(
      path.join(tmpDir, 'packs', '@tapestry', 'core', 'tapestry.yaml'),
      'name: "@tapestry/core"\nversion: "1.0.0"\n'
    );

    await install(undefined, { cwd: tmpDir });

    expect(extractTarball).not.toHaveBeenCalled();
  });

  it('writes boot file after install', async () => {
    writeProjectManifest(tmpDir, { '@tapestry/core': '^1.0.0' });
    fetchPackageMetadata.mockResolvedValue(makeRegistryMeta('@tapestry/core', '1.0.0'));
    fetchTarball.mockResolvedValue(Buffer.from('fake'));

    await install(undefined, { cwd: tmpDir });

    const boot = readBoot(tmpDir);
    expect(boot.packs['@tapestry/core']).toEqual({ enabled: true });
  });

  it('succeeds with no dependencies in tapestry.yaml', async () => {
    writeProjectManifest(tmpDir, {});

    await install(undefined, { cwd: tmpDir });

    expect(fetchPackageMetadata).not.toHaveBeenCalled();
  });
});

describe('install @scope/name (with package arg)', () => {
  it('adds package to tapestry.yaml with resolved version range', async () => {
    writeProjectManifest(tmpDir, {});
    fetchPackageMetadata.mockResolvedValue(makeRegistryMeta('@tapestry/weather', '0.8.1'));
    fetchTarball.mockResolvedValue(Buffer.from('fake'));

    await install('@tapestry/weather', { cwd: tmpDir });

    const updated = readYaml(path.join(tmpDir, 'tapestry.yaml'));
    expect(updated.dependencies['@tapestry/weather']).toBe('^0.8.1');
  });

  it('respects explicit range when provided', async () => {
    writeProjectManifest(tmpDir, {});
    fetchPackageMetadata.mockResolvedValue(makeRegistryMeta('@tapestry/weather', '0.8.1'));
    fetchTarball.mockResolvedValue(Buffer.from('fake'));

    await install('@tapestry/weather@~0.8.0', { cwd: tmpDir });

    const updated = readYaml(path.join(tmpDir, 'tapestry.yaml'));
    expect(updated.dependencies['@tapestry/weather']).toBe('~0.8.0');
  });

  it('throws on invalid package name format', async () => {
    writeProjectManifest(tmpDir, {});
    await expect(install('not-scoped', { cwd: tmpDir })).rejects.toThrow('Invalid package name');
  });
});

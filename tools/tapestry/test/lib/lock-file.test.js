'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { readLock, writeLock } = require('../../src/lib/lock-file');

let tmpDir;

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-lock-'));
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

describe('readLock', () => {
  it('returns null when no lock file exists', () => {
    expect(readLock(tmpDir)).toBeNull();
  });

  it('reads an existing lock file', () => {
    const lock = {
      lockfile_version: 1,
      resolved: {
        '@tapestry/core': { version: '1.0.0', integrity: 'sha256-abc', tarball: 'https://example.com/core.tgz' },
      },
    };
    writeLock(tmpDir, lock);

    const result = readLock(tmpDir);

    expect(result.lockfile_version).toBe(1);
    expect(result.resolved['@tapestry/core'].version).toBe('1.0.0');
  });
});

describe('writeLock', () => {
  it('writes tapestry-lock.yaml', () => {
    const resolved = {
      '@tapestry/core': { version: '1.0.0', integrity: 'sha256-abc', tarball: 'https://example.com/core.tgz' },
    };
    writeLock(tmpDir, { lockfile_version: 1, resolved });

    expect(fs.existsSync(path.join(tmpDir, 'tapestry-lock.yaml'))).toBe(true);
  });

  it('round-trips resolved data accurately', () => {
    const resolved = {
      '@tapestry/core': { version: '1.2.3', integrity: 'sha256-xyz', tarball: 'https://x.com/a.tgz' },
      '@tapestry/weather': { version: '0.8.1', integrity: 'sha256-def', tarball: 'https://x.com/b.tgz' },
    };
    writeLock(tmpDir, { lockfile_version: 1, resolved });
    const result = readLock(tmpDir);

    expect(result.resolved['@tapestry/weather'].version).toBe('0.8.1');
    expect(result.resolved['@tapestry/weather'].integrity).toBe('sha256-def');
  });
});

'use strict';

const crypto = require('crypto');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { verifyIntegrity, saveTarball, extractTarball } = require('../../src/lib/tarball');

let tmpDir;

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-tarball-'));
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

describe('verifyIntegrity', () => {
  it('passes when hash matches', () => {
    const buf = Buffer.from('test content');
    const hash = crypto.createHash('sha256').update(buf).digest('base64');
    const integrity = `sha256-${hash}`;

    expect(() => verifyIntegrity(buf, integrity)).not.toThrow();
  });

  it('throws when hash does not match', () => {
    const buf = Buffer.from('test content');
    expect(() => verifyIntegrity(buf, 'sha256-wronghash==')).toThrow('Integrity check failed');
  });
});

describe('saveTarball', () => {
  it('writes buffer to disk at given path', () => {
    const buf = Buffer.from('tarball bytes');
    const dest = path.join(tmpDir, 'pkg.tgz');

    saveTarball(buf, dest);

    expect(fs.existsSync(dest)).toBe(true);
    expect(fs.readFileSync(dest)).toEqual(buf);
  });
});

describe('extractTarball', () => {
  it('extracts a tarball into destDir stripping the top-level directory', async () => {
    const tar = require('tar');
    const srcDir = path.join(tmpDir, 'src');
    const pkgDir = path.join(srcDir, 'package');
    fs.mkdirSync(pkgDir, { recursive: true });
    fs.writeFileSync(path.join(pkgDir, 'tapestry.yaml'), 'name: "@test/pkg"\nversion: "1.0.0"\n');

    const tgzPath = path.join(tmpDir, 'pkg.tgz');
    await tar.create({ gzip: true, file: tgzPath, cwd: srcDir }, ['package']);

    const destDir = path.join(tmpDir, 'dest');
    await extractTarball(tgzPath, destDir);

    expect(fs.existsSync(path.join(destDir, 'tapestry.yaml'))).toBe(true);
  });
});

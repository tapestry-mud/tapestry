'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const tar = require('tar');
const { buildTarball, computeIntegrity } = require('../../src/lib/tarball-builder');

let tmpDir;

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-tarbuild-'));
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

function makePack(dir) {
  fs.writeFileSync(path.join(dir, 'tapestry.yaml'), 'name: "@mallek/test"\nversion: "1.0.0"\n');
  fs.writeFileSync(path.join(dir, 'readme.md'), 'hello');
  const subDir = path.join(dir, 'scripts');
  fs.mkdirSync(subDir);
  fs.writeFileSync(path.join(subDir, 'init.js'), 'console.log("hi")');
}

it('creates a .tgz file at the output path', async () => {
  const packDir = path.join(tmpDir, 'my-pack');
  fs.mkdirSync(packDir);
  makePack(packDir);

  const outputPath = path.join(tmpDir, 'my-pack-1.0.0.tgz');
  await buildTarball(packDir, outputPath);

  expect(fs.existsSync(outputPath)).toBe(true);
});

it('tarball entries are prefixed with package/', async () => {
  const packDir = path.join(tmpDir, 'my-pack');
  fs.mkdirSync(packDir);
  makePack(packDir);

  const outputPath = path.join(tmpDir, 'out.tgz');
  await buildTarball(packDir, outputPath);

  const entries = [];
  await tar.list({
    file: outputPath,
    onentry: (entry) => entries.push(entry.path),
  });

  expect(entries.some((e) => e.startsWith('package/'))).toBe(true);
  expect(entries.some((e) => e.includes('tapestry.yaml'))).toBe(true);
});

it('excludes .git and node_modules directories', async () => {
  const packDir = path.join(tmpDir, 'my-pack');
  fs.mkdirSync(packDir);
  makePack(packDir);

  fs.mkdirSync(path.join(packDir, '.git'));
  fs.writeFileSync(path.join(packDir, '.git', 'HEAD'), 'ref: refs/heads/master');
  fs.mkdirSync(path.join(packDir, 'node_modules'));
  fs.writeFileSync(path.join(packDir, 'node_modules', 'dep.js'), 'module.exports={}');

  const outputPath = path.join(tmpDir, 'out.tgz');
  await buildTarball(packDir, outputPath);

  const entries = [];
  await tar.list({
    file: outputPath,
    onentry: (entry) => entries.push(entry.path),
  });

  expect(entries.every((e) => !e.includes('.git'))).toBe(true);
  expect(entries.every((e) => !e.includes('node_modules'))).toBe(true);
});

it('computeIntegrity returns sha256-<base64> format', async () => {
  const filePath = path.join(tmpDir, 'test.bin');
  fs.writeFileSync(filePath, 'some content');

  const integrity = computeIntegrity(filePath);

  expect(integrity).toMatch(/^sha256-[A-Za-z0-9+/]+=*$/);
});

it('same file content produces same integrity hash', async () => {
  const path1 = path.join(tmpDir, 'a.bin');
  const path2 = path.join(tmpDir, 'b.bin');
  fs.writeFileSync(path1, 'identical content');
  fs.writeFileSync(path2, 'identical content');

  expect(computeIntegrity(path1)).toBe(computeIntegrity(path2));
});

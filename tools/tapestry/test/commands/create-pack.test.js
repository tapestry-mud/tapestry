'use strict';

const { createPack, parseName } = require('../../src/commands/create-pack');
const fs = require('fs');
const path = require('path');
const os = require('os');

let tmpDir;

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-create-'));
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

describe('parseName', () => {
  test('parses a scoped name', () => {
    expect(parseName('@author/my-pack')).toEqual({
      scopedName: '@author/my-pack',
      shortName: 'my-pack',
      scope: 'author',
    });
  });

  test('parses a plain name and fills TODO scope placeholder', () => {
    expect(parseName('my-pack')).toEqual({
      scopedName: '@todo/my-pack',
      shortName: 'my-pack',
      scope: 'todo',
    });
  });

  test('returns null for a name with spaces', () => {
    expect(parseName('My Pack')).toBeNull();
  });

  test('returns null for a scoped name without a package part', () => {
    expect(parseName('@no-slash')).toBeNull();
  });
});

describe('createPack', () => {
  test('creates a directory named after the short name', () => {
    createPack('@author/my-pack', tmpDir);
    expect(fs.existsSync(path.join(tmpDir, 'my-pack'))).toBe(true);
  });

  test('tapestry.yaml contains the scoped name', () => {
    createPack('@author/my-pack', tmpDir);
    const content = fs.readFileSync(path.join(tmpDir, 'my-pack', 'tapestry.yaml'), 'utf8');
    expect(content).toContain('@author/my-pack');
  });

  test('creates all 8 scaffold files', () => {
    createPack('@author/my-pack', tmpDir);
    const packDir = path.join(tmpDir, 'my-pack');
    expect(fs.existsSync(path.join(packDir, 'tags.yml'))).toBe(true);
    expect(fs.existsSync(path.join(packDir, 'areas', 'example-area', 'area.yaml'))).toBe(true);
    expect(fs.existsSync(path.join(packDir, 'areas', 'example-area', 'rooms', 'town-square.yaml'))).toBe(true);
    expect(fs.existsSync(path.join(packDir, 'areas', 'example-area', 'mobs', 'guard.yaml'))).toBe(true);
    expect(fs.existsSync(path.join(packDir, 'areas', 'example-area', 'items', 'lantern.yaml'))).toBe(true);
    expect(fs.existsSync(path.join(packDir, 'scripts', 'init.js'))).toBe(true);
    expect(fs.existsSync(path.join(packDir, 'help', 'example.yaml'))).toBe(true);
  });

  test('throws if the target directory already exists', () => {
    fs.mkdirSync(path.join(tmpDir, 'my-pack'));
    expect(() => { createPack('@author/my-pack', tmpDir); }).toThrow('already exists');
  });

  test('throws for an invalid pack name', () => {
    expect(() => { createPack('My Invalid Pack', tmpDir); }).toThrow('Invalid pack name');
  });
});

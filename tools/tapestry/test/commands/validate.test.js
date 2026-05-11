'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { writeYaml } = require('../../src/util/yaml');
const { validate } = require('../../src/commands/validate');

let tmpDir;

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-validate-'));
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

function writeManifest(cwd, data) {
  writeYaml(path.join(cwd, 'tapestry.yaml'), data);
}

const VALID_MANIFEST = {
  name: '@mallek/test-pack',
  version: '1.0.0',
  type: 'module',
  display_name: 'Test Pack',
  description: 'A test pack',
  author: { name: 'Tester', handle: 'mallek' },
  license: 'MIT',
  engine: '>=3.0.0',
  tag_validation: 'strict',
};

it('succeeds on a valid manifest', () => {
  writeManifest(tmpDir, VALID_MANIFEST);
  expect(() => validate({ cwd: tmpDir })).not.toThrow();
});

it('throws when tapestry.yaml is missing', () => {
  expect(() => validate({ cwd: tmpDir })).toThrow('No tapestry.yaml found');
});

it('throws when name is not scoped', () => {
  writeManifest(tmpDir, { ...VALID_MANIFEST, name: 'bad-name' });
  expect(() => validate({ cwd: tmpDir })).toThrow(/validation error/);
});

it('throws when required field is missing', () => {
  const { description: _d, ...rest } = VALID_MANIFEST;
  writeManifest(tmpDir, rest);
  expect(() => validate({ cwd: tmpDir })).toThrow(/validation error/);
});

it('throws when type is invalid', () => {
  writeManifest(tmpDir, { ...VALID_MANIFEST, type: 'plugin' });
  expect(() => validate({ cwd: tmpDir })).toThrow(/validation error/);
});

it('throws when tag_validation is invalid', () => {
  writeManifest(tmpDir, { ...VALID_MANIFEST, tag_validation: 'none' });
  expect(() => validate({ cwd: tmpDir })).toThrow(/validation error/);
});

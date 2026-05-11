'use strict';

const { readYaml, writeYaml } = require('../../src/util/yaml');
const fs = require('fs');
const path = require('path');
const os = require('os');

let tmpDir;

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-yaml-'));
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

test('roundtrips a plain object through YAML', () => {
  const data = { name: 'test', version: '1.0.0', nested: { key: 'value' } };
  const file = path.join(tmpDir, 'test.yaml');
  writeYaml(file, data);
  const result = readYaml(file);
  expect(result).toEqual(data);
});

test('writeYaml produces a text file containing the values', () => {
  const data = { name: '@author/my-pack', version: '0.1.0' };
  const file = path.join(tmpDir, 'manifest.yaml');
  writeYaml(file, data);
  const raw = fs.readFileSync(file, 'utf8');
  expect(raw).toContain('@author/my-pack');
});

'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');

jest.mock('../../src/lib/tarball-builder');
jest.mock('../../src/commands/validate');

const { buildTarball, computeIntegrity } = require('../../src/lib/tarball-builder');
const { validate } = require('../../src/commands/validate');
const { writeYaml } = require('../../src/util/yaml');
const { pack } = require('../../src/commands/pack');

let tmpDir;

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-pack-'));
  jest.clearAllMocks();
  buildTarball.mockResolvedValue(undefined);
  computeIntegrity.mockReturnValue('sha256-abc123');
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

it('runs validate before building', async () => {
  writeYaml(path.join(tmpDir, 'tapestry.yaml'), {
    name: '@mallek/test-pack',
    version: '0.2.0',
  });

  await pack({ cwd: tmpDir });

  expect(validate).toHaveBeenCalledWith({ cwd: tmpDir });
  expect(buildTarball).toHaveBeenCalled();
});

it('names the output tarball <short-name>-<version>.tgz', async () => {
  writeYaml(path.join(tmpDir, 'tapestry.yaml'), {
    name: '@mallek/combat-utils',
    version: '1.2.3',
  });

  await pack({ cwd: tmpDir });

  const [, outputPath] = buildTarball.mock.calls[0];
  expect(path.basename(outputPath)).toBe('combat-utils-1.2.3.tgz');
});

it('writes the tarball into the pack directory', async () => {
  writeYaml(path.join(tmpDir, 'tapestry.yaml'), {
    name: '@mallek/test-pack',
    version: '0.1.0',
  });

  await pack({ cwd: tmpDir });

  const [, outputPath] = buildTarball.mock.calls[0];
  expect(path.dirname(outputPath)).toBe(tmpDir);
});

it('throws if validate throws', async () => {
  validate.mockImplementation(() => {
    throw new Error('2 validation error(s)');
  });

  await expect(pack({ cwd: tmpDir })).rejects.toThrow('2 validation error(s)');
  expect(buildTarball).not.toHaveBeenCalled();
});

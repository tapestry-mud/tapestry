'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { disable } = require('../../src/commands/disable');
const { writeBoot, readBoot } = require('../../src/lib/boot');
const { writeYaml } = require('../../src/util/yaml');

let tmpDir;

function setupBoot(cwd, packs) {
  writeYaml(path.join(cwd, 'tapestry.yaml'), { name: 'test', engine: '>=3.0.0', dependencies: {} });
  writeBoot(cwd, { modules: [], packs });
}

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-disable-'));
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

describe('disable', () => {
  it('throws when tapestry.yaml is missing', async () => {
    await expect(disable('@tapestry/weather', { cwd: tmpDir })).rejects.toThrow('No tapestry.yaml found');
  });

  it('disables an enabled package', async () => {
    setupBoot(tmpDir, { '@tapestry/weather': { enabled: true } });

    await disable('@tapestry/weather', { cwd: tmpDir });

    expect(readBoot(tmpDir).packs['@tapestry/weather'].enabled).toBe(false);
  });

  it('throws when package is not installed', async () => {
    setupBoot(tmpDir, {});
    await expect(disable('@tapestry/missing', { cwd: tmpDir })).rejects.toThrow('@tapestry/missing is not installed');
  });

  it('is idempotent when package is already disabled', async () => {
    setupBoot(tmpDir, { '@tapestry/weather': { enabled: false } });
    await expect(disable('@tapestry/weather', { cwd: tmpDir })).resolves.not.toThrow();
    expect(readBoot(tmpDir).packs['@tapestry/weather'].enabled).toBe(false);
  });
});

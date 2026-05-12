'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');

jest.mock('child_process', () => ({
  spawnSync: jest.fn(() => ({ status: 0 })),
  spawn: jest.fn(() => ({ pid: 9999, unref: jest.fn() })),
}));

const { spawnSync, spawn } = require('child_process');
const { writeYaml } = require('../../src/util/yaml');
const {
  installEngine,
  updateEngine,
  getEngineInfo,
  startEngine,
  stopEngine,
} = require('../../src/lib/engine-manager');

let tmpDir;

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-em-'));
  spawnSync.mockClear();
  spawn.mockClear();
  spawn.mockReturnValue({ pid: 9999, unref: jest.fn() });
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

// ── readEngineConfig ────────────────────────────────────────────────────────

describe('missing or invalid engine config', () => {
  it('throws when tapestry.yaml is missing', async () => {
    await expect(installEngine(tmpDir)).rejects.toThrow('No tapestry.yaml found');
  });

  it('throws when engine field is a plain string', async () => {
    writeYaml(path.join(tmpDir, 'tapestry.yaml'), { name: 'my-game', engine: '>=3.0.0' });
    await expect(installEngine(tmpDir)).rejects.toThrow('engine must be configured as an object');
  });

  it('throws when engine.version is missing', async () => {
    writeYaml(path.join(tmpDir, 'tapestry.yaml'), { name: 'my-game', engine: { mode: 'docker' } });
    await expect(installEngine(tmpDir)).rejects.toThrow('engine.version is required');
  });

  it('throws when engine.mode is invalid', async () => {
    writeYaml(path.join(tmpDir, 'tapestry.yaml'), {
      name: 'my-game',
      engine: { version: '3.1.0', mode: 'kubernetes' },
    });
    await expect(installEngine(tmpDir)).rejects.toThrow(
      'engine.mode must be docker, binary, or source'
    );
  });
});

describe('readEngineConfig — valid config', () => {
  it('returns correct shape for a valid docker manifest', async () => {
    writeYaml(path.join(tmpDir, 'tapestry.yaml'), {
      name: 'my-game',
      engine: { version: '3.1.0', mode: 'docker' },
    });
    const info = getEngineInfo(tmpDir);
    expect(info).toMatchObject({
      version: '3.1.0',
      mode: 'docker',
      image: 'ghcr.io/tapestry-mud/tapestry',
      projectName: 'my-game',
    });
    expect(info.installDir).toContain('.tapestry-engine');
  });
});

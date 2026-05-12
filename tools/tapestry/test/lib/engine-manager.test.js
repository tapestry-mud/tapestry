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
      image: 'ghcr.io/tapestry-mud/tapestry:3.1.0',
    });
  });
});

// ── Docker mode ────────────────────────────────────────────────────────────

describe('docker mode', () => {
  beforeEach(() => {
    writeYaml(path.join(tmpDir, 'tapestry.yaml'), {
      name: 'my-game',
      engine: { version: '3.1.0', mode: 'docker', image: 'ghcr.io/tapestry-mud/tapestry' },
    });
  });

  describe('installEngine', () => {
    it('calls docker pull with the configured image and version', async () => {
      await installEngine(tmpDir);
      expect(spawnSync).toHaveBeenCalledWith(
        'docker', ['pull', 'ghcr.io/tapestry-mud/tapestry:3.1.0'],
        { stdio: 'inherit' }
      );
    });

    it('uses the default image when engine.image is not set', async () => {
      writeYaml(path.join(tmpDir, 'tapestry.yaml'), {
        name: 'my-game',
        engine: { version: '3.1.0', mode: 'docker' },
      });
      await installEngine(tmpDir);
      expect(spawnSync).toHaveBeenCalledWith(
        'docker', ['pull', 'ghcr.io/tapestry-mud/tapestry:3.1.0'],
        { stdio: 'inherit' }
      );
    });

    it('throws when docker pull fails', async () => {
      spawnSync.mockReturnValueOnce({ status: 1 });
      await expect(installEngine(tmpDir)).rejects.toThrow('docker pull failed');
    });
  });

  describe('updateEngine', () => {
    it('calls docker pull (same as install)', async () => {
      await updateEngine(tmpDir);
      expect(spawnSync).toHaveBeenCalledWith(
        'docker', ['pull', 'ghcr.io/tapestry-mud/tapestry:3.1.0'],
        { stdio: 'inherit' }
      );
    });
  });

  describe('getEngineInfo', () => {
    it('returns mode, version, and full image tag', () => {
      const info = getEngineInfo(tmpDir);
      expect(info).toMatchObject({
        mode: 'docker',
        version: '3.1.0',
        image: 'ghcr.io/tapestry-mud/tapestry:3.1.0',
      });
    });
  });

  describe('startEngine', () => {
    beforeEach(() => {
      fs.mkdirSync(path.join(tmpDir, 'packs'), { recursive: true });
      fs.writeFileSync(path.join(tmpDir, 'server.yaml'), 'port: 4000\n');
    });

    it('calls docker run with detach, container name, ports, and image', async () => {
      await startEngine(tmpDir);
      expect(spawnSync).toHaveBeenCalledWith(
        'docker',
        expect.arrayContaining([
          'run', '--detach',
          '--name', 'tapestry-my-game',
          '-p', '4000:4000',
          '-p', '4001:4001',
          'ghcr.io/tapestry-mud/tapestry:3.1.0',
        ]),
        { stdio: 'inherit' }
      );
    });

    it('mounts packs/ as a volume at /app/packs', async () => {
      await startEngine(tmpDir);
      const args = spawnSync.mock.calls[0][1];
      const volArgs = args.filter((_, i) => args[i - 1] === '-v');
      expect(volArgs.some(v => v.endsWith(':/app/packs'))).toBe(true);
    });

    it('mounts server.yaml as a volume at /app/server.yaml', async () => {
      await startEngine(tmpDir);
      const args = spawnSync.mock.calls[0][1];
      const volArgs = args.filter((_, i) => args[i - 1] === '-v');
      expect(volArgs.some(v => v.endsWith(':/app/server.yaml'))).toBe(true);
    });

    it('throws when packs/ directory does not exist', async () => {
      fs.rmSync(path.join(tmpDir, 'packs'), { recursive: true });
      await expect(startEngine(tmpDir)).rejects.toThrow('packs/ directory not found');
    });

    it('throws when server.yaml does not exist', async () => {
      fs.rmSync(path.join(tmpDir, 'server.yaml'));
      await expect(startEngine(tmpDir)).rejects.toThrow('server.yaml not found');
    });

    it('throws when docker run fails', async () => {
      spawnSync.mockReturnValueOnce({ status: 1 });
      await expect(startEngine(tmpDir)).rejects.toThrow('docker run failed');
    });
  });

  describe('stopEngine', () => {
    it('calls docker stop then docker rm with the container name', async () => {
      await stopEngine(tmpDir);
      expect(spawnSync).toHaveBeenCalledWith(
        'docker', ['stop', 'tapestry-my-game'], { stdio: 'inherit' }
      );
      expect(spawnSync).toHaveBeenCalledWith(
        'docker', ['rm', 'tapestry-my-game'], { stdio: 'inherit' }
      );
    });

    it('throws a clear message when docker stop fails', async () => {
      spawnSync.mockReturnValueOnce({ status: 1 });
      await expect(stopEngine(tmpDir)).rejects.toThrow(
        "Failed to stop container 'tapestry-my-game'"
      );
    });
  });
});

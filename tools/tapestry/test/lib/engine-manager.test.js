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

    it('throws when docker rm fails', async () => {
      spawnSync
        .mockReturnValueOnce({ status: 0 })  // docker stop succeeds
        .mockReturnValueOnce({ status: 1 }); // docker rm fails
      await expect(stopEngine(tmpDir)).rejects.toThrow("Failed to remove container");
    });
  });
});

// ── Binary mode ────────────────────────────────────────────────────────────

describe('binary mode', () => {
  beforeEach(() => {
    writeYaml(path.join(tmpDir, 'tapestry.yaml'), {
      name: 'my-game',
      engine: { version: '3.1.0', mode: 'binary' },
    });
  });

  describe('installEngine', () => {
    it('creates the install directory, calls curl then tar', async () => {
      await installEngine(tmpDir);

      const calls = spawnSync.mock.calls;
      expect(calls[0][0]).toBe('curl');
      expect(calls[0][1].join(' ')).toContain('3.1.0');
      expect(calls[1][0]).toBe('tar');
      expect(calls[1][1]).toContain('-xzf');
    });

    it('downloads the platform-specific archive', async () => {
      await installEngine(tmpDir);

      const curlArgs = spawnSync.mock.calls[0][1].join(' ');
      const platform = { linux: 'linux', darwin: 'osx', win32: 'windows' }[process.platform] || 'linux';
      expect(curlArgs).toContain(`tapestry-${platform}.tar.gz`);
    });

    it('throws when curl fails', async () => {
      spawnSync.mockReturnValueOnce({ status: 1 });
      await expect(installEngine(tmpDir)).rejects.toThrow('Failed to download engine binary');
    });

    it('throws when tar fails', async () => {
      spawnSync
        .mockReturnValueOnce({ status: 0 })  // curl succeeds
        .mockReturnValueOnce({ status: 1 }); // tar fails
      await expect(installEngine(tmpDir)).rejects.toThrow('Failed to extract engine binary');
    });
  });

  describe('getEngineInfo', () => {
    it('returns mode, version, path, and installed: false when not downloaded', () => {
      const info = getEngineInfo(tmpDir);
      expect(info.mode).toBe('binary');
      expect(info.version).toBe('3.1.0');
      expect(info.installed).toBe(false);
      expect(info.path).toContain(path.join('.tapestry-engine', 'binary', '3.1.0'));
    });

    it('returns installed: true when the binary directory exists', () => {
      const binDir = path.join(tmpDir, '.tapestry-engine', 'binary', '3.1.0');
      fs.mkdirSync(binDir, { recursive: true });
      expect(getEngineInfo(tmpDir).installed).toBe(true);
    });
  });

  describe('startEngine', () => {
    let binDir;
    let execName;

    beforeEach(() => {
      binDir = path.join(tmpDir, '.tapestry-engine', 'binary', '3.1.0');
      execName = process.platform === 'win32' ? 'Tapestry.exe' : 'Tapestry';
      fs.mkdirSync(binDir, { recursive: true });
      fs.writeFileSync(path.join(binDir, execName), '#!/bin/sh\n');
      fs.mkdirSync(path.join(tmpDir, 'packs'), { recursive: true });
      fs.writeFileSync(path.join(tmpDir, 'server.yaml'), 'port: 4000\n');
    });

    it('spawns the binary with --packs and --config flags', async () => {
      await startEngine(tmpDir);

      const [cmd, args] = spawn.mock.calls[0];
      expect(cmd).toContain(execName);
      expect(args).toContain('--packs');
      expect(args).toContain('--config');
    });

    it('spawns with detached: true and calls unref()', async () => {
      const mockChild = { pid: 9999, unref: jest.fn() };
      spawn.mockReturnValueOnce(mockChild);

      await startEngine(tmpDir);

      const opts = spawn.mock.calls[0][2];
      expect(opts.detached).toBe(true);
      expect(mockChild.unref).toHaveBeenCalled();
    });

    it('writes the child PID to .tapestry.pid', async () => {
      const mockChild = { pid: 9999, unref: jest.fn() };
      spawn.mockReturnValueOnce(mockChild);

      await startEngine(tmpDir);

      const { readPid } = require('../../src/lib/process-tracker');
      expect(readPid(tmpDir)).toBe(9999);
    });

    it('throws when the binary does not exist', async () => {
      fs.rmSync(path.join(binDir, execName));
      await expect(startEngine(tmpDir)).rejects.toThrow('Engine binary not found');
    });
  });

  describe('stopEngine', () => {
    it('kills the process by PID and clears the pid file', async () => {
      const { writePid, readPid } = require('../../src/lib/process-tracker');
      writePid(tmpDir, 9999);
      const killSpy = jest.spyOn(process, 'kill').mockImplementation(() => {});

      await stopEngine(tmpDir);

      expect(killSpy).toHaveBeenCalledWith(9999, 'SIGTERM');
      expect(readPid(tmpDir)).toBeNull();
      killSpy.mockRestore();
    });

    it('throws when no pid file exists', async () => {
      await expect(stopEngine(tmpDir)).rejects.toThrow('Engine is not running');
    });

    it('still clears pid file when process is already gone', async () => {
      const { writePid, readPid } = require('../../src/lib/process-tracker');
      writePid(tmpDir, 9999);
      const killSpy = jest.spyOn(process, 'kill').mockImplementation(() => {
        const err = new Error('ESRCH');
        err.code = 'ESRCH';
        throw err;
      });

      await stopEngine(tmpDir);

      expect(readPid(tmpDir)).toBeNull();
      killSpy.mockRestore();
    });
  });
});

// ── Source mode ────────────────────────────────────────────────────────────

describe('source mode', () => {
  beforeEach(() => {
    writeYaml(path.join(tmpDir, 'tapestry.yaml'), {
      name: 'my-game',
      engine: { version: '3.1.0', mode: 'source' },
    });
  });

  describe('installEngine', () => {
    it('calls git clone into .tapestry-engine/source', async () => {
      await installEngine(tmpDir);

      expect(spawnSync).toHaveBeenCalledWith(
        'git',
        expect.arrayContaining(['clone', expect.stringContaining('tapestry')]),
        { stdio: 'inherit' }
      );
      const cloneArgs = spawnSync.mock.calls[0][1];
      expect(cloneArgs[cloneArgs.length - 1]).toContain(
        path.join('.tapestry-engine', 'source')
      );
    });

    it('throws when source directory already exists', async () => {
      const sourceDir = path.join(tmpDir, '.tapestry-engine', 'source');
      fs.mkdirSync(sourceDir, { recursive: true });
      await expect(installEngine(tmpDir)).rejects.toThrow('already exists');
    });

    it('throws when git clone fails', async () => {
      spawnSync.mockReturnValueOnce({ status: 1 });
      await expect(installEngine(tmpDir)).rejects.toThrow('git clone failed');
    });
  });

  describe('updateEngine', () => {
    it('calls git pull in the source directory', async () => {
      const sourceDir = path.join(tmpDir, '.tapestry-engine', 'source');
      fs.mkdirSync(sourceDir, { recursive: true });

      await updateEngine(tmpDir);

      const [cmd, args] = spawnSync.mock.calls[0];
      expect(cmd).toBe('git');
      expect(args).toContain('pull');
      expect(args.join(' ')).toContain('.tapestry-engine');
    });

    it('throws when source directory does not exist', async () => {
      await expect(updateEngine(tmpDir)).rejects.toThrow('Engine source not found');
    });

    it('throws when git pull fails', async () => {
      const sourceDir = path.join(tmpDir, '.tapestry-engine', 'source');
      fs.mkdirSync(sourceDir, { recursive: true });
      spawnSync.mockReturnValueOnce({ status: 1 });
      await expect(updateEngine(tmpDir)).rejects.toThrow('git pull failed');
    });
  });

  describe('getEngineInfo', () => {
    it('returns mode, version, path, and installed: false when not cloned', () => {
      const info = getEngineInfo(tmpDir);
      expect(info.mode).toBe('source');
      expect(info.version).toBe('3.1.0');
      expect(info.installed).toBe(false);
      expect(info.path).toContain(path.join('.tapestry-engine', 'source'));
    });

    it('returns installed: true when source directory exists', () => {
      const sourceDir = path.join(tmpDir, '.tapestry-engine', 'source');
      fs.mkdirSync(sourceDir, { recursive: true });
      expect(getEngineInfo(tmpDir).installed).toBe(true);
    });
  });

  describe('startEngine', () => {
    beforeEach(() => {
      const sourceDir = path.join(tmpDir, '.tapestry-engine', 'source');
      fs.mkdirSync(sourceDir, { recursive: true });
      fs.mkdirSync(path.join(tmpDir, 'packs'), { recursive: true });
      fs.writeFileSync(path.join(tmpDir, 'server.yaml'), 'port: 4000\n');
    });

    it('spawns dotnet run in the source directory with --packs and --config', async () => {
      const mockChild = { pid: 8888, unref: jest.fn() };
      spawn.mockReturnValueOnce(mockChild);

      await startEngine(tmpDir);

      const [cmd, args, opts] = spawn.mock.calls[0];
      expect(cmd).toBe('dotnet');
      expect(args[0]).toBe('run');
      expect(args).toContain('--packs');
      expect(args).toContain('--config');
      expect(opts.cwd).toContain(path.join('.tapestry-engine', 'source'));
    });

    it('spawns with detached: true and calls unref()', async () => {
      const mockChild = { pid: 8888, unref: jest.fn() };
      spawn.mockReturnValueOnce(mockChild);

      await startEngine(tmpDir);

      const opts = spawn.mock.calls[0][2];
      expect(opts.detached).toBe(true);
      expect(mockChild.unref).toHaveBeenCalled();
    });

    it('writes the child PID to .tapestry.pid', async () => {
      const mockChild = { pid: 8888, unref: jest.fn() };
      spawn.mockReturnValueOnce(mockChild);

      await startEngine(tmpDir);

      const { readPid } = require('../../src/lib/process-tracker');
      expect(readPid(tmpDir)).toBe(8888);
    });

    it('throws when source directory does not exist', async () => {
      fs.rmSync(path.join(tmpDir, '.tapestry-engine', 'source'), { recursive: true });
      await expect(startEngine(tmpDir)).rejects.toThrow('Engine source not found');
    });
  });

  describe('stopEngine', () => {
    it('kills the process by PID and clears the pid file', async () => {
      const { writePid, readPid } = require('../../src/lib/process-tracker');
      writePid(tmpDir, 8888);
      const killSpy = jest.spyOn(process, 'kill').mockImplementation(() => {});

      await stopEngine(tmpDir);

      expect(killSpy).toHaveBeenCalledWith(8888, 'SIGTERM');
      expect(readPid(tmpDir)).toBeNull();
      killSpy.mockRestore();
    });

    it('throws when no pid file exists', async () => {
      await expect(stopEngine(tmpDir)).rejects.toThrow('Engine is not running');
    });
  });
});

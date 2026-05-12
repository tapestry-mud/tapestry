'use strict';

jest.mock('../../src/lib/engine-manager');

const { installEngine, updateEngine, getEngineInfo } = require('../../src/lib/engine-manager');
const { engineInstall, engineUpdate, engineInfo } = require('../../src/commands/engine');

beforeEach(() => {
  installEngine.mockReset();
  updateEngine.mockReset();
  getEngineInfo.mockReset();
});

describe('engineInstall', () => {
  it('calls installEngine with the provided cwd', async () => {
    installEngine.mockResolvedValue();
    await engineInstall({ cwd: '/fake/cwd' });
    expect(installEngine).toHaveBeenCalledWith('/fake/cwd');
  });

  it('propagates errors from installEngine', async () => {
    installEngine.mockRejectedValue(new Error('docker pull failed'));
    await expect(engineInstall({ cwd: '/fake/cwd' })).rejects.toThrow('docker pull failed');
  });
});

describe('engineUpdate', () => {
  it('calls updateEngine with the provided cwd', async () => {
    updateEngine.mockResolvedValue();
    await engineUpdate({ cwd: '/fake/cwd' });
    expect(updateEngine).toHaveBeenCalledWith('/fake/cwd');
  });

  it('propagates errors from updateEngine', async () => {
    updateEngine.mockRejectedValue(new Error('git pull failed'));
    await expect(engineUpdate({ cwd: '/fake/cwd' })).rejects.toThrow('git pull failed');
  });
});

describe('engineInfo', () => {
  it('prints mode, version, and image for docker mode', () => {
    getEngineInfo.mockReturnValue({
      mode: 'docker',
      version: '3.1.0',
      image: 'ghcr.io/tapestry-mud/tapestry:3.1.0',
    });
    const spy = jest.spyOn(console, 'log').mockImplementation(() => {});

    engineInfo({ cwd: '/fake/cwd' });

    expect(spy).toHaveBeenCalledWith('Mode:    docker');
    expect(spy).toHaveBeenCalledWith('Version: 3.1.0');
    expect(spy).toHaveBeenCalledWith('Image:   ghcr.io/tapestry-mud/tapestry:3.1.0');
    spy.mockRestore();
  });

  it('prints mode, version, path, and installed status for binary mode', () => {
    getEngineInfo.mockReturnValue({
      mode: 'binary',
      version: '3.1.0',
      path: '/tmp/my-game/.tapestry-engine/binary/3.1.0',
      installed: true,
    });
    const spy = jest.spyOn(console, 'log').mockImplementation(() => {});

    engineInfo({ cwd: '/fake/cwd' });

    expect(spy).toHaveBeenCalledWith('Mode:    binary');
    expect(spy).toHaveBeenCalledWith('Version: 3.1.0');
    expect(spy).toHaveBeenCalledWith('Status:  installed');
    spy.mockRestore();
  });

  it('prints "not installed" when binary is missing', () => {
    getEngineInfo.mockReturnValue({
      mode: 'binary',
      version: '3.1.0',
      path: '/tmp/my-game/.tapestry-engine/binary/3.1.0',
      installed: false,
    });
    const spy = jest.spyOn(console, 'log').mockImplementation(() => {});

    engineInfo({ cwd: '/fake/cwd' });

    expect(spy).toHaveBeenCalledWith('Status:  not installed');
    spy.mockRestore();
  });
});

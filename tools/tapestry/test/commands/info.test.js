'use strict';

jest.mock('../../src/lib/registry-client');

const { fetchPackageMetadata } = require('../../src/lib/registry-client');
const { info } = require('../../src/commands/info');

beforeEach(() => {
  jest.clearAllMocks();
});

const PACKAGE_DATA = {
  name: '@tapestry/weather',
  owner: 'mallek',
  versions: [
    {
      version: '0.8.1',
      manifest: {
        name: '@tapestry/weather',
        version: '0.8.1',
        type: 'module',
        description: 'Dynamic weather patterns',
        author: { name: 'Travis', handle: 'mallek' },
        license: 'MIT',
        dependencies: { '@tapestry/core': '^1.0.0' },
        meta: { keywords: ['weather', 'environment'] },
      },
      integrity: 'sha256-abc',
    },
    {
      version: '0.8.0',
      manifest: { name: '@tapestry/weather', version: '0.8.0' },
      integrity: 'sha256-def',
    },
  ],
};

it('throws when package name is missing', async () => {
  await expect(info()).rejects.toThrow('Usage: tapestry info <package>');
  expect(fetchPackageMetadata).not.toHaveBeenCalled();
});

it('fetches package metadata from registry', async () => {
  fetchPackageMetadata.mockResolvedValue(PACKAGE_DATA);
  await info('@tapestry/weather');
  expect(fetchPackageMetadata).toHaveBeenCalledWith('@tapestry/weather', undefined);
});

it('prints package name and description', async () => {
  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  fetchPackageMetadata.mockResolvedValue(PACKAGE_DATA);

  await info('@tapestry/weather');

  const output = spy.mock.calls.map((c) => c[0]).join('\n');
  expect(output).toContain('@tapestry/weather');
  expect(output).toContain('Dynamic weather patterns');
  spy.mockRestore();
});

it('prints author, license, type, and latest version', async () => {
  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  fetchPackageMetadata.mockResolvedValue(PACKAGE_DATA);

  await info('@tapestry/weather');

  const output = spy.mock.calls.map((c) => c[0]).join('\n');
  expect(output).toContain('mallek');
  expect(output).toContain('MIT');
  expect(output).toContain('module');
  expect(output).toContain('0.8.1');
  spy.mockRestore();
});

it('prints dependencies when present', async () => {
  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  fetchPackageMetadata.mockResolvedValue(PACKAGE_DATA);

  await info('@tapestry/weather');

  const output = spy.mock.calls.map((c) => c[0]).join('\n');
  expect(output).toContain('@tapestry/core');
  spy.mockRestore();
});

it('propagates registry errors', async () => {
  fetchPackageMetadata.mockRejectedValue(new Error('Package @bad/pkg not found in registry'));
  await expect(info('@bad/pkg')).rejects.toThrow('not found in registry');
});

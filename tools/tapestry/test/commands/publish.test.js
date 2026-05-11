'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');

jest.mock('node-fetch');
jest.mock('../../src/lib/auth');
jest.mock('../../src/lib/tarball-builder');
jest.mock('../../src/commands/validate');

const fetch = require('node-fetch');
const { requireToken } = require('../../src/lib/auth');
const { buildTarball, computeIntegrity } = require('../../src/lib/tarball-builder');
const { validate } = require('../../src/commands/validate');
const { writeYaml } = require('../../src/util/yaml');
const { publish } = require('../../src/commands/publish');

let tmpDir;

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-publish-'));
  jest.clearAllMocks();

  validate.mockImplementation(() => {});
  requireToken.mockReturnValue('test-token');
  computeIntegrity.mockReturnValue('sha256-abc123');

  buildTarball.mockImplementation(async (packDir, outputPath) => {
    fs.writeFileSync(outputPath, 'dummy tarball content');
  });

  fetch.mockResolvedValue({
    ok: true,
    status: 201,
    json: async () => ({ name: '@mallek/test', version: '1.0.0', integrity: 'sha256-abc123' }),
  });
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

function writeManifest(cwd) {
  writeYaml(path.join(cwd, 'tapestry.yaml'), {
    name: '@mallek/test-pack',
    version: '1.0.0',
    type: 'module',
    display_name: 'Test',
    description: 'A test',
    author: { name: 'Tester', handle: 'mallek' },
    license: 'MIT',
    engine: '>=3.0.0',
    tag_validation: 'strict',
  });
}

it('runs validate before publishing', async () => {
  writeManifest(tmpDir);
  await publish({ cwd: tmpDir });
  expect(validate).toHaveBeenCalledWith({ cwd: tmpDir });
});

it('requires auth token', async () => {
  writeManifest(tmpDir);
  requireToken.mockImplementation(() => {
    throw new Error('Not logged in. Run: tapestry login');
  });
  await expect(publish({ cwd: tmpDir })).rejects.toThrow('Not logged in');
});

it('sends multipart POST to /v1/publish with Authorization header', async () => {
  writeManifest(tmpDir);
  await publish({ cwd: tmpDir });

  expect(fetch).toHaveBeenCalledWith(
    expect.stringContaining('/v1/publish'),
    expect.objectContaining({
      method: 'POST',
      headers: expect.objectContaining({
        Authorization: 'Bearer test-token',
      }),
    })
  );
});

it('throws on server error', async () => {
  writeManifest(tmpDir);
  fetch.mockResolvedValue({
    ok: false,
    status: 403,
    json: async () => ({ error: 'scope @mallek is not owned by @other' }),
  });

  await expect(publish({ cwd: tmpDir })).rejects.toThrow('scope @mallek is not owned by @other');
});

it('throws if validate fails', async () => {
  validate.mockImplementation(() => {
    throw new Error('1 validation error(s)');
  });
  await expect(publish({ cwd: tmpDir })).rejects.toThrow('1 validation error(s)');
  expect(fetch).not.toHaveBeenCalled();
});

it('uses custom registryUrl when provided', async () => {
  writeManifest(tmpDir);
  await publish({ cwd: tmpDir, registryUrl: 'http://localhost:3002' });
  expect(fetch).toHaveBeenCalledWith(
    'http://localhost:3002/v1/publish',
    expect.anything()
  );
});

'use strict';

jest.mock('../../src/lib/registry-client');
const { fetchPackageMetadata } = require('../../src/lib/registry-client');
const { resolve } = require('../../src/lib/semver-resolver');

function makePackageMeta(name, versions) {
  return {
    name,
    versions: versions.map(({ version, deps }) => ({
      version,
      integrity: `sha256-fake-${version}`,
      manifest: { name, version, dependencies: deps || {} },
    })),
  };
}

describe('resolve', () => {
  beforeEach(() => {
    fetchPackageMetadata.mockReset();
  });

  it('resolves a single direct dependency to highest satisfying version', async () => {
    fetchPackageMetadata.mockResolvedValue(
      makePackageMeta('@tapestry/core', [{ version: '1.0.0' }, { version: '1.1.0' }, { version: '2.0.0' }])
    );

    const result = await resolve({ '@tapestry/core': '^1.0.0' }, 'http://localhost:3002');

    expect(result['@tapestry/core'].version).toBe('1.1.0');
    expect(result['@tapestry/core'].integrity).toBe('sha256-fake-1.1.0');
    expect(result['@tapestry/core'].tarball).toContain('@tapestry/core/1.1.0.tgz');
  });

  it('resolves transitive dependencies', async () => {
    fetchPackageMetadata
      .mockResolvedValueOnce(
        makePackageMeta('@tapestry/combat-skills', [{ version: '0.9.0', deps: { '@tapestry/core': '^1.0.0' } }])
      )
      .mockResolvedValueOnce(
        makePackageMeta('@tapestry/core', [{ version: '1.0.0' }])
      );

    const result = await resolve({ '@tapestry/combat-skills': '^0.9.0' }, 'http://localhost:3002');

    expect(result['@tapestry/combat-skills'].version).toBe('0.9.0');
    expect(result['@tapestry/core'].version).toBe('1.0.0');
  });

  it('deduplicates shared transitive deps', async () => {
    fetchPackageMetadata
      .mockResolvedValueOnce(
        makePackageMeta('@tapestry/a', [{ version: '1.0.0', deps: { '@tapestry/core': '^1.0.0' } }])
      )
      .mockResolvedValueOnce(
        makePackageMeta('@tapestry/b', [{ version: '1.0.0', deps: { '@tapestry/core': '^1.0.0' } }])
      )
      .mockResolvedValueOnce(
        makePackageMeta('@tapestry/core', [{ version: '1.2.0' }])
      );

    const result = await resolve(
      { '@tapestry/a': '^1.0.0', '@tapestry/b': '^1.0.0' },
      'http://localhost:3002'
    );

    expect(Object.keys(result)).toHaveLength(3);
    expect(fetchPackageMetadata).toHaveBeenCalledTimes(3);
  });

  it('errors when two ranges conflict on the same package', async () => {
    fetchPackageMetadata
      .mockResolvedValueOnce(
        makePackageMeta('@tapestry/a', [{ version: '1.0.0', deps: { '@tapestry/core': '^1.0.0' } }])
      )
      .mockResolvedValueOnce(
        makePackageMeta('@tapestry/b', [{ version: '1.0.0', deps: { '@tapestry/core': '^2.0.0' } }])
      )
      .mockResolvedValueOnce(
        makePackageMeta('@tapestry/core', [{ version: '1.0.0' }])
      );

    await expect(
      resolve({ '@tapestry/a': '^1.0.0', '@tapestry/b': '^1.0.0' }, 'http://localhost:3002')
    ).rejects.toThrow('CONFLICT');
  });

  it('errors when no version satisfies the range', async () => {
    fetchPackageMetadata.mockResolvedValue(
      makePackageMeta('@tapestry/core', [{ version: '1.0.0' }, { version: '1.1.0' }])
    );

    await expect(
      resolve({ '@tapestry/core': '^2.0.0' }, 'http://localhost:3002')
    ).rejects.toThrow('No version of @tapestry/core satisfies ^2.0.0');
  });

  it('returns empty object for empty dependencies', async () => {
    const result = await resolve({}, 'http://localhost:3002');
    expect(result).toEqual({});
    expect(fetchPackageMetadata).not.toHaveBeenCalled();
  });

  it('handles undefined dependencies gracefully', async () => {
    const result = await resolve(undefined, 'http://localhost:3002');
    expect(result).toEqual({});
  });

  it('warns about missing peer dependencies', async () => {
    const warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => {});
    fetchPackageMetadata.mockResolvedValue({
      name: '@tapestry/combat-skills',
      versions: [{
        version: '1.0.0',
        integrity: 'sha256-x',
        manifest: {
          name: '@tapestry/combat-skills',
          version: '1.0.0',
          dependencies: {},
          peerDependencies: { '@tapestry/sustenance': '^1.0.0' },
        },
      }],
    });

    await resolve({ '@tapestry/combat-skills': '^1.0.0' }, 'http://localhost:3002');

    expect(warnSpy).toHaveBeenCalledWith(expect.stringContaining('@tapestry/sustenance'));
    warnSpy.mockRestore();
  });
});

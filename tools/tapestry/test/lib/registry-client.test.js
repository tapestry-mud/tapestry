'use strict';

const { fetchPackageMetadata, fetchTarball, DEFAULT_REGISTRY } = require('../../src/lib/registry-client');

jest.mock('node-fetch');
const fetch = require('node-fetch');

describe('registry-client', () => {
  beforeEach(() => {
    fetch.mockReset();
  });

  describe('DEFAULT_REGISTRY', () => {
    it('uses env var when set', () => {
      const orig = process.env.TAPESTRY_REGISTRY;
      process.env.TAPESTRY_REGISTRY = 'http://myregistry.test';
      jest.resetModules();
      const { DEFAULT_REGISTRY: dr } = require('../../src/lib/registry-client');
      expect(dr).toBe('http://myregistry.test');
      if (orig === undefined) {
        delete process.env.TAPESTRY_REGISTRY;
      } else {
        process.env.TAPESTRY_REGISTRY = orig;
      }
    });
  });

  describe('fetchPackageMetadata', () => {
    it('fetches package metadata from correct URL', async () => {
      const meta = { name: '@tapestry/core', versions: [] };
      fetch.mockResolvedValue({ ok: true, json: () => Promise.resolve(meta) });

      const result = await fetchPackageMetadata('@tapestry/core', 'http://localhost:3002');

      expect(fetch).toHaveBeenCalledWith('http://localhost:3002/v1/packages/@tapestry/core');
      expect(result).toEqual(meta);
    });

    it('throws on 404', async () => {
      fetch.mockResolvedValue({ ok: false, status: 404, text: () => Promise.resolve('not found') });

      await expect(fetchPackageMetadata('@tapestry/missing', 'http://localhost:3002'))
        .rejects.toThrow('Package @tapestry/missing not found in registry');
    });

    it('throws on non-404 error', async () => {
      fetch.mockResolvedValue({ ok: false, status: 500, text: () => Promise.resolve('oops') });

      await expect(fetchPackageMetadata('@tapestry/core', 'http://localhost:3002'))
        .rejects.toThrow('Registry error 500');
    });
  });

  describe('fetchTarball', () => {
    it('returns buffer on success', async () => {
      const buf = Buffer.from('fake tarball');
      fetch.mockResolvedValue({
        ok: true,
        buffer: () => Promise.resolve(buf),
      });

      const result = await fetchTarball('http://localhost:3002/v1/packages/@tapestry/core/1.0.0.tgz');

      expect(result).toEqual(buf);
    });

    it('throws on download failure', async () => {
      fetch.mockResolvedValue({ ok: false, status: 403, text: () => Promise.resolve('forbidden') });

      await expect(fetchTarball('http://localhost:3002/bad.tgz'))
        .rejects.toThrow('Tarball download failed: 403');
    });
  });

  describe('fetchPackageMetadata input validation', () => {
    it('throws on invalid package name format', async () => {
      await expect(fetchPackageMetadata('not-scoped', 'http://localhost:3002'))
        .rejects.toThrow('Invalid package name');
    });

    it('throws on path traversal attempt', async () => {
      await expect(fetchPackageMetadata('@scope/../../../etc/passwd', 'http://localhost:3002'))
        .rejects.toThrow('Invalid package name');
    });

    it('throws on empty string', async () => {
      await expect(fetchPackageMetadata('', 'http://localhost:3002'))
        .rejects.toThrow('Package name must be a non-empty string');
    });
  });
});

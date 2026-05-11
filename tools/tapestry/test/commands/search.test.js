'use strict';

jest.mock('node-fetch');

const fetch = require('node-fetch');
const { search } = require('../../src/commands/search');

beforeEach(() => {
  jest.clearAllMocks();
});

it('throws when query is empty', async () => {
  await expect(search('')).rejects.toThrow('Usage: tapestry search <query>');
  expect(fetch).not.toHaveBeenCalled();
});

it('fetches search endpoint with encoded query', async () => {
  fetch.mockResolvedValue({
    ok: true,
    json: async () => ({ results: [] }),
  });

  await search('combat skills');

  expect(fetch).toHaveBeenCalledWith(
    expect.stringContaining('q=combat%20skills'),
    undefined
  );
});

it('prints a result row for each match', async () => {
  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  fetch.mockResolvedValue({
    ok: true,
    json: async () => ({
      results: [
        { name: '@tapestry/combat', version: '1.0.0', description: 'Combat system' },
        { name: '@tapestry/pvp', version: '0.4.0', description: 'PvP arena' },
      ],
    }),
  });

  await search('combat');

  const output = spy.mock.calls.map((c) => c[0]).join('\n');
  expect(output).toContain('@tapestry/combat');
  expect(output).toContain('1.0.0');
  expect(output).toContain('@tapestry/pvp');
  spy.mockRestore();
});

it('prints no results message when empty', async () => {
  const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
  fetch.mockResolvedValue({
    ok: true,
    json: async () => ({ results: [] }),
  });

  await search('xyznotfound');

  expect(spy).toHaveBeenCalledWith('No results found.');
  spy.mockRestore();
});

it('throws on registry error', async () => {
  fetch.mockResolvedValue({
    ok: false,
    status: 500,
    text: async () => 'internal error',
  });

  await expect(search('test')).rejects.toThrow('Search failed (500)');
});

it('uses custom registryUrl when provided', async () => {
  fetch.mockResolvedValue({
    ok: true,
    json: async () => ({ results: [] }),
  });

  await search('test', { registryUrl: 'http://localhost:3002' });

  expect(fetch).toHaveBeenCalledWith(
    expect.stringContaining('http://localhost:3002'),
    undefined
  );
});

'use strict';

jest.mock('node-fetch');
jest.mock('../../src/lib/auth');

const fetch = require('node-fetch');
const { saveToken } = require('../../src/lib/auth');
const { register } = require('../../src/commands/register');

beforeEach(() => {
  jest.clearAllMocks();
});

it('posts registration and saves token on success', async () => {
  const consoleSpy = jest.spyOn(console, 'log').mockImplementation(() => {});

  fetch.mockResolvedValue({
    ok: true,
    status: 201,
    json: async () => ({ token: 'new-jwt' }),
  });

  await register({ handle: 'mallek', email: 'me@example.com', password: 'pass' });

  expect(fetch).toHaveBeenCalledWith(
    expect.stringContaining('/v1/auth/register'),
    expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ handle: 'mallek', email: 'me@example.com', password: 'pass' }),
    })
  );
  expect(saveToken).toHaveBeenCalledWith('new-jwt');
  expect(consoleSpy).toHaveBeenCalledWith('Registered as mallek. Logged in.');

  consoleSpy.mockRestore();
});

it('throws on conflict (handle taken)', async () => {
  fetch.mockResolvedValue({
    ok: false,
    status: 409,
    json: async () => ({ error: 'handle or email already taken' }),
  });

  await expect(
    register({ handle: 'taken', email: 'x@x.com', password: 'pw' })
  ).rejects.toThrow('handle or email already taken');
  expect(saveToken).not.toHaveBeenCalled();
});

it('throws generic message when server returns no error field', async () => {
  fetch.mockResolvedValue({
    ok: false,
    status: 500,
    json: async () => ({}),
  });

  await expect(
    register({ handle: 'h', email: 'e@e.com', password: 'p' })
  ).rejects.toThrow('Registration failed (500)');
});

it('uses custom registryUrl when provided', async () => {
  fetch.mockResolvedValue({
    ok: true,
    status: 201,
    json: async () => ({ token: 'tok' }),
  });

  await register(
    { handle: 'h', email: 'a@b.com', password: 'x' },
    { registryUrl: 'http://localhost:3002' }
  );

  expect(fetch).toHaveBeenCalledWith(
    'http://localhost:3002/v1/auth/register',
    expect.anything()
  );
});

'use strict';

jest.mock('node-fetch');
jest.mock('../../src/lib/auth');

const fetch = require('node-fetch');
const { saveToken } = require('../../src/lib/auth');
const { login } = require('../../src/commands/login');

beforeEach(() => {
  jest.clearAllMocks();
});

it('posts credentials and saves token on success', async () => {
  const consoleSpy = jest.spyOn(console, 'log').mockImplementation(() => {});

  fetch.mockResolvedValue({
    ok: true,
    json: async () => ({ token: 'jwt-abc' }),
  });

  await login({ email: 'user@example.com', password: 'secret' });

  expect(fetch).toHaveBeenCalledWith(
    expect.stringContaining('/v1/auth/login'),
    expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ email: 'user@example.com', password: 'secret' }),
    })
  );
  expect(saveToken).toHaveBeenCalledWith('jwt-abc');
  expect(consoleSpy).toHaveBeenCalledWith('Logged in.');

  consoleSpy.mockRestore();
});

it('throws on invalid credentials', async () => {
  fetch.mockResolvedValue({
    ok: false,
    status: 401,
    json: async () => ({ error: 'invalid credentials' }),
  });

  await expect(
    login({ email: 'bad@example.com', password: 'wrong' })
  ).rejects.toThrow('invalid credentials');
  expect(saveToken).not.toHaveBeenCalled();
});

it('throws generic message when error body has no error field', async () => {
  fetch.mockResolvedValue({
    ok: false,
    status: 500,
    json: async () => ({}),
  });

  await expect(
    login({ email: 'user@example.com', password: 'pass' })
  ).rejects.toThrow('Login failed (500)');
});

it('uses custom registryUrl when provided', async () => {
  fetch.mockResolvedValue({
    ok: true,
    json: async () => ({ token: 'tok' }),
  });

  await login(
    { email: 'a@b.com', password: 'x' },
    { registryUrl: 'http://localhost:3002' }
  );

  expect(fetch).toHaveBeenCalledWith(
    'http://localhost:3002/v1/auth/login',
    expect.anything()
  );
});

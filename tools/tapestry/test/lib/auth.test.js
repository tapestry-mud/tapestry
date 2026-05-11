'use strict';

const fs = require('fs');
const path = require('path');

const { loadToken, saveToken, requireToken, RC_PATH } = require('../../src/lib/auth');

describe('loadToken', () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('returns null when rc file does not exist', () => {
    jest.spyOn(fs, 'existsSync').mockReturnValue(false);
    expect(loadToken()).toBeNull();
  });

  it('returns token from rc file', () => {
    jest.spyOn(fs, 'existsSync').mockReturnValue(true);
    jest.spyOn(fs, 'readFileSync').mockReturnValue('token: my-jwt\n');
    expect(loadToken()).toBe('my-jwt');
  });

  it('returns null when rc file is malformed', () => {
    jest.spyOn(fs, 'existsSync').mockReturnValue(true);
    jest.spyOn(fs, 'readFileSync').mockImplementation(() => {
      throw new Error('read error');
    });
    expect(loadToken()).toBeNull();
  });

  it('returns null when rc file has no token field', () => {
    jest.spyOn(fs, 'existsSync').mockReturnValue(true);
    jest.spyOn(fs, 'readFileSync').mockReturnValue('other: value\n');
    expect(loadToken()).toBeNull();
  });
});

describe('saveToken', () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('writes YAML with token to RC_PATH', () => {
    const spy = jest.spyOn(fs, 'writeFileSync').mockImplementation(() => {});
    saveToken('abc-jwt');
    expect(spy).toHaveBeenCalledWith(RC_PATH, expect.stringContaining('abc-jwt'), { mode: 0o600 });
  });
});

describe('requireToken', () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('throws when not logged in', () => {
    jest.spyOn(fs, 'existsSync').mockReturnValue(false);
    expect(() => requireToken()).toThrow('Not logged in. Run: tapestry login');
  });

  it('returns token when logged in', () => {
    jest.spyOn(fs, 'existsSync').mockReturnValue(true);
    jest.spyOn(fs, 'readFileSync').mockReturnValue('token: valid-jwt\n');
    expect(requireToken()).toBe('valid-jwt');
  });
});

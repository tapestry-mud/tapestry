'use strict';

jest.mock('../../src/lib/engine-manager');

const { stopEngine } = require('../../src/lib/engine-manager');
const { stopCmd } = require('../../src/commands/stop');

beforeEach(() => {
  stopEngine.mockReset();
});

it('calls stopEngine with the provided cwd', async () => {
  stopEngine.mockResolvedValue();
  await stopCmd({ cwd: '/fake/cwd' });
  expect(stopEngine).toHaveBeenCalledWith('/fake/cwd');
});

it('propagates errors from stopEngine', async () => {
  stopEngine.mockRejectedValue(new Error('Engine is not running'));
  await expect(stopCmd({ cwd: '/fake/cwd' })).rejects.toThrow('Engine is not running');
});

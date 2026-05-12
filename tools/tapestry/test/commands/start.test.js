'use strict';

jest.mock('../../src/lib/engine-manager');

const { startEngine } = require('../../src/lib/engine-manager');
const { startCmd } = require('../../src/commands/start');

beforeEach(() => {
  startEngine.mockReset();
});

it('calls startEngine with the provided cwd', async () => {
  startEngine.mockResolvedValue();
  await startCmd({ cwd: '/fake/cwd' });
  expect(startEngine).toHaveBeenCalledWith('/fake/cwd');
});

it('propagates errors from startEngine', async () => {
  startEngine.mockRejectedValue(new Error('packs/ directory not found'));
  await expect(startCmd({ cwd: '/fake/cwd' })).rejects.toThrow('packs/ directory not found');
});

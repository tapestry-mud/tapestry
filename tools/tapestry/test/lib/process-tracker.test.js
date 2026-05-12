'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { writePid, readPid, clearPid } = require('../../src/lib/process-tracker');

let tmpDir;

beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-pt-'));
});

afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true });
});

it('writes and reads a PID', () => {
  writePid(tmpDir, 12345);
  expect(readPid(tmpDir)).toBe(12345);
});

it('returns null when no pid file exists', () => {
  expect(readPid(tmpDir)).toBeNull();
});

it('clearPid removes the pid file', () => {
  writePid(tmpDir, 12345);
  clearPid(tmpDir);
  expect(readPid(tmpDir)).toBeNull();
});

it('clearPid is a no-op when no file exists', () => {
  expect(() => clearPid(tmpDir)).not.toThrow();
});

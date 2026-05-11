const os = require('os');
const fs = require('fs');
const path = require('path');
const yaml = require('js-yaml');
const { loadConfig, getLimitsForScope } = require('../src/config');

let tmpDir;
beforeEach(() => {
  tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tapestry-config-'));
});
afterEach(() => {
  fs.rmSync(tmpDir, { recursive: true, force: true });
});

function writeConfig(obj) {
  const file = path.join(tmpDir, 'registry-config.yaml');
  fs.writeFileSync(file, yaml.dump(obj));
  return file;
}

test('returns defaults when config file does not exist', () => {
  const config = loadConfig('/nonexistent/path/registry-config.yaml');
  expect(config.limits.default.max_tarball_mb).toBe(2);
  expect(config.limits.default.max_versions).toBe(20);
  expect(config.limits.default.max_scope_mb).toBe(50);
  expect(config.limits.total_cap_gb).toBe(10);
  expect(config.bypass).toEqual([]);
  expect(config.overrides).toEqual({});
});

test('loads and merges custom config', () => {
  const file = writeConfig({ limits: { default: { max_tarball_mb: 5 } } });
  const config = loadConfig(file);
  expect(config.limits.default.max_tarball_mb).toBe(5);
  expect(config.limits.default.max_versions).toBe(20); // default preserved
});

test('getLimitsForScope returns defaults for unknown scope', () => {
  const config = loadConfig('/nonexistent/path.yaml');
  const limits = getLimitsForScope(config, 'somedev');
  expect(limits.max_tarball_mb).toBe(2);
  expect(limits.max_versions).toBe(20);
});

test('getLimitsForScope returns null for bypassed scope', () => {
  const file = writeConfig({ bypass: ['@tapestry', '@mallek'] });
  const config = loadConfig(file);
  expect(getLimitsForScope(config, '@tapestry')).toBeNull();
  expect(getLimitsForScope(config, '@mallek')).toBeNull();
  expect(getLimitsForScope(config, '@somedev')).not.toBeNull();
});

test('getLimitsForScope applies per-scope overrides', () => {
  const file = writeConfig({
    overrides: { '@somedev': { max_tarball_mb: 10, max_scope_mb: 150 } },
  });
  const config = loadConfig(file);
  const limits = getLimitsForScope(config, '@somedev');
  expect(limits.max_tarball_mb).toBe(10);
  expect(limits.max_scope_mb).toBe(150);
  expect(limits.max_versions).toBe(20); // default preserved
});

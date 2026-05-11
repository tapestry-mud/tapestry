const { computeIntegrity } = require('../src/integrity');

test('computeIntegrity returns sha256-<base64> format', () => {
  const buf = Buffer.from('hello');
  const result = computeIntegrity(buf);
  expect(result).toMatch(/^sha256-[A-Za-z0-9+/]+=*$/);
});

test('computeIntegrity is deterministic', () => {
  const buf = Buffer.from('same content');
  expect(computeIntegrity(buf)).toBe(computeIntegrity(buf));
});

test('different content produces different hash', () => {
  expect(computeIntegrity(Buffer.from('a'))).not.toBe(computeIntegrity(Buffer.from('b')));
});

const { checkPublishLimits } = require('../src/routes/publishRoutes');
const { initDb } = require('../src/db');
const { loadConfig } = require('../src/config');

describe('checkPublishLimits', () => {
  let db;
  const defaultConfig = loadConfig('/nonexistent/path.yaml');

  beforeEach(() => { db = initDb(':memory:'); });
  afterEach(() => { db.close(); });

  function seedVersions(db, count) {
    db.prepare(`INSERT INTO packages (scope, name, owner_handle) VALUES ('testscope', 'testpkg', 'owner')`).run();
    const pkg = db.prepare(`SELECT id FROM packages WHERE scope = 'testscope' AND name = 'testpkg'`).get();
    for (let i = 0; i < count; i++) {
      db.prepare(`INSERT INTO versions (package_id, version, manifest, tarball_path, tarball_size, integrity) VALUES (?, ?, '{}', '/tmp/x.tgz', 1024, 'sha256-x')`).run(pkg.id, `1.${i}.0`);
    }
    return pkg;
  }

  test('returns null when under all limits', () => {
    const result = checkPublishLimits(db, defaultConfig, '@testscope', 'testpkg', 500 * 1024);
    expect(result).toBeNull();
  });

  test('rejects tarball over max_tarball_mb', () => {
    const result = checkPublishLimits(db, defaultConfig, '@testscope', 'testpkg', 3 * 1024 * 1024);
    expect(result.error).toMatch(/tarball/i);
  });

  test('rejects when version count at max', () => {
    seedVersions(db, 20);
    const result = checkPublishLimits(db, defaultConfig, '@testscope', 'testpkg', 1024);
    expect(result.error).toMatch(/version/i);
  });

  test('bypassed scope ignores all limits', () => {
    const config = { ...defaultConfig, bypass: ['@testscope'] };
    const result = checkPublishLimits(db, config, '@testscope', 'testpkg', 100 * 1024 * 1024);
    expect(result).toBeNull();
  });

  test('rejects when scope storage over max_scope_mb', () => {
    db.prepare(`INSERT INTO packages (scope, name, owner_handle) VALUES ('testscope', 'testpkg', 'owner')`).run();
    const pkg = db.prepare(`SELECT id FROM packages WHERE scope = 'testscope' AND name = 'testpkg'`).get();
    const bigSize = 50 * 1024 * 1024; // 50MB existing -- at the scope cap
    db.prepare(`INSERT INTO versions (package_id, version, manifest, tarball_path, tarball_size, integrity) VALUES (?, '1.0.0', '{}', '/tmp/x.tgz', ?, 'sha256-x')`).run(pkg.id, bigSize);
    const result = checkPublishLimits(db, defaultConfig, '@testscope', 'testpkg', 1); // any new bytes tip it over
    expect(result.error).toMatch(/storage/i);
  });

  test('rejects new package when scope is already at storage limit', () => {
    // Create a DIFFERENT package in the same scope that fills the storage
    db.prepare(`INSERT INTO packages (scope, name, owner_handle) VALUES ('testscope', 'otherpkg', 'owner')`).run();
    const otherPkg = db.prepare(`SELECT id FROM packages WHERE scope = 'testscope' AND name = 'otherpkg'`).get();
    const bigSize = 50 * 1024 * 1024; // exactly at 50MB limit
    db.prepare(`INSERT INTO versions (package_id, version, manifest, tarball_path, tarball_size, integrity) VALUES (?, '1.0.0', '{}', '/tmp/x.tgz', ?, 'sha256-x')`).run(otherPkg.id, bigSize);

    // Try to publish a new package ('testpkg' doesn't exist yet) - should fail on scope storage
    const result = checkPublishLimits(db, defaultConfig, '@testscope', 'testpkg', 1024);
    expect(result.error).toMatch(/storage/i);
  });
});

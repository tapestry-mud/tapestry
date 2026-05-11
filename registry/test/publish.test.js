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

const request = require('supertest');
const path = require('path');
const fs = require('fs');
const { createTestApp, cleanupTestApp, seedAccount } = require('./helpers');
const { signToken } = require('../src/auth');

describe('POST /v1/publish', () => {
  let publishApp, publishDb, publishDataDir, token;

  beforeEach(() => {
    ({ app: publishApp, db: publishDb, dataDir: publishDataDir } = createTestApp());
    seedAccount(publishDb, { handle: 'mallek', email: 'mallek@example.com' });
    token = signToken({ handle: 'mallek', email: 'mallek@example.com' });
  });
  afterEach(() => cleanupTestApp({ db: publishDb, dataDir: publishDataDir }));

  function makeManifest(overrides = {}) {
    return JSON.stringify({
      name: '@mallek/testpkg',
      version: '1.0.0',
      description: 'A test package',
      type: 'module',
      author: { name: 'Test', handle: 'mallek' },
      license: 'MIT',
      engine: '>=3.0.0',
      tag_validation: 'strict',
      ...overrides,
    });
  }

  test('publishes a new package version', async () => {
    const res = await request(publishApp)
      .post('/v1/publish')
      .set('Authorization', `Bearer ${token}`)
      .attach('tarball', Buffer.from('fake-tarball'), 'package.tgz')
      .field('metadata', makeManifest());
    expect(res.status).toBe(201);
    expect(res.body.name).toBe('@mallek/testpkg');
    expect(res.body.version).toBe('1.0.0');
    expect(res.body.integrity).toMatch(/^sha256-/);
  });

  test('stores tarball on filesystem', async () => {
    await request(publishApp)
      .post('/v1/publish')
      .set('Authorization', `Bearer ${token}`)
      .attach('tarball', Buffer.from('fake-tarball'), 'package.tgz')
      .field('metadata', makeManifest());
    const tgzPath = path.join(publishDataDir, 'packages', '@mallek', 'testpkg', '1.0.0.tgz');
    expect(fs.existsSync(tgzPath)).toBe(true);
  });

  test('rejects duplicate version', async () => {
    const payload = () => request(publishApp)
      .post('/v1/publish')
      .set('Authorization', `Bearer ${token}`)
      .attach('tarball', Buffer.from('fake-tarball'), 'package.tgz')
      .field('metadata', makeManifest());
    await payload();
    const res = await payload();
    expect(res.status).toBe(409);
  });

  test('rejects publish to wrong scope', async () => {
    const res = await request(publishApp)
      .post('/v1/publish')
      .set('Authorization', `Bearer ${token}`)
      .attach('tarball', Buffer.from('fake-tarball'), 'package.tgz')
      .field('metadata', makeManifest({ name: '@tapestry/testpkg' }));
    expect(res.status).toBe(403);
  });

  test('rejects unauthenticated publish', async () => {
    const res = await request(publishApp)
      .post('/v1/publish')
      .attach('tarball', Buffer.from('fake-tarball'), 'package.tgz')
      .field('metadata', makeManifest());
    expect(res.status).toBe(401);
  });

  test('rejects missing required manifest fields', async () => {
    const res = await request(publishApp)
      .post('/v1/publish')
      .set('Authorization', `Bearer ${token}`)
      .attach('tarball', Buffer.from('fake-tarball'), 'package.tgz')
      .field('metadata', JSON.stringify({ name: '@mallek/testpkg' })); // no version
    expect(res.status).toBe(400);
  });

  test('rejects over-size tarball', async () => {
    const bigBuffer = Buffer.alloc(3 * 1024 * 1024, 'x'); // 3MB, over 2MB default
    const res = await request(publishApp)
      .post('/v1/publish')
      .set('Authorization', `Bearer ${token}`)
      .attach('tarball', bigBuffer, 'package.tgz')
      .field('metadata', makeManifest());
    expect(res.status).toBe(400);
    expect(res.body.error).toMatch(/tarball/i);
  });
});

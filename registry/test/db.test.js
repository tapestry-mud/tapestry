const { initDb } = require('../src/db');

let db;

beforeEach(() => {
  db = initDb(':memory:');
});

afterEach(() => {
  db.close();
});

test('creates accounts table', () => {
  const row = db.prepare(`SELECT name FROM sqlite_master WHERE type='table' AND name='accounts'`).get();
  expect(row).toBeTruthy();
});

test('creates packages table', () => {
  const row = db.prepare(`SELECT name FROM sqlite_master WHERE type='table' AND name='packages'`).get();
  expect(row).toBeTruthy();
});

test('creates versions table', () => {
  const row = db.prepare(`SELECT name FROM sqlite_master WHERE type='table' AND name='versions'`).get();
  expect(row).toBeTruthy();
});

test('enforces unique scope+name in packages', () => {
  db.prepare(`INSERT INTO packages (scope, name, owner_handle) VALUES ('tapestry', 'core', 'admin')`).run();
  expect(() => {
    db.prepare(`INSERT INTO packages (scope, name, owner_handle) VALUES ('tapestry', 'core', 'admin')`).run();
  }).toThrow(/UNIQUE/);
});

test('foreign key enforced on versions', () => {
  expect(() => {
    db.prepare(`INSERT INTO versions (package_id, version, manifest, tarball_path, tarball_size, integrity) VALUES (999, '1.0.0', '{}', '/tmp/x.tgz', 100, 'sha256-abc')`).run();
  }).toThrow(/FOREIGN KEY/);
});
